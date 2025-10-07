#define _CRT_SECURE_NO_WARNINGS
#include "Utilities.h"
#include <sstream>
#include <ctime>

std::string GetCurrentTimestamp() {
    time_t now = time(0);
    struct tm tstruct;
    char buf[80];
    localtime_s(&tstruct, &now);
    strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%S", &tstruct);
    return std::string(buf);
}

std::string EscapeJsonString(const std::string& input) {
    std::string output;
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

std::string CreateJsonPacket(const std::string& messageType, const std::string& payloadType, const std::string& content) {
    extern const char* BOT_ID;
    std::stringstream json;
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
#pragma once
#include <string>

std::string GetCurrentTimestamp();
std::string EscapeJsonString(const std::string& input);
std::string CreateJsonPacket(const std::string& messageType, const std::string& payloadType, const std::string& content);

