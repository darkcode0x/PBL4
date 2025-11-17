#pragma once
#include "IClient.h"
#include "Botnet.h"
#include <mutex>
#include <iomanip>
#include <iostream>
#include <sstream>
class ConsoleClient : public IClient {
public:
    ConsoleClient(){};
    void Send(const std::string& rawBytes, bool isError) override {

        std::string hexStr;
        {
            std::ostringstream oss;
            for (unsigned char c : rawBytes) {
                oss << std::hex << std::setw(2) << std::setfill('0') << (int)c;
            }
            hexStr = oss.str();
        }
        
        {
            std::lock_guard<std::mutex> lk(_mtx);
            if (isError) std::cout << "[ERR HEX] ";
            else std::cout << "[OUT/IN HEX] ";
            std::cout << hexStr << '\n';
        }
        
        // Gui raw bytes den queue
        EnqueueSend(rawBytes);
    }
private:
    std::mutex _mtx;
};