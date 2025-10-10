#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include "Network.h"
#include <iostream>
#include <string>

using namespace std;

int main() {
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
    return 0;
}
