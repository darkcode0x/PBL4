#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include "Network.h"
#include <iostream>
#include <string>

using namespace std;

int main() {
    std::cout << "=== DNS Tunnel Client (manual input mode) ===" << std::endl;
    std::cout << "Bot ID: " << BOT_ID << std::endl;
    std::cout << "Server: " << SERVER_IP << ":" << SERVER_PORT << std::endl;
    std::cout << "Type lines and press Enter to send. Type 'exit' to quit." << std::endl << std::endl;

    if (!InitializeConnection()) {
        std::cerr << "Failed to initialize connection. Exiting..." << std::endl;
        return 1;
    }

    SendHeartbeat();

    std::string line;
    while (true) {
        std::cout << "> ";
        if (!std::getline(std::cin, line)) break;
        if (line == "exit") break;
        if (line.empty()) continue;
        SendManualInput(line);
    }

    if (clientSocket != INVALID_SOCKET) closesocket(clientSocket);
    WSACleanup();

    std::cout << "Client exiting." << std::endl;
    return 0;
}
