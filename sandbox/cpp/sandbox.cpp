#include "stdafx.h"

#include "sandbox.h"
#include "restd.h"

VOID DumpResource(LPCWSTR resourceName)
{
  OutputDebugString(L"DumpResource(");
  OutputDebugString(resourceName);
  OutputDebugString(L")\n");

  HANDLE resourceHandle = CreateFile(resourceName, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL /*| FILE_FLAG_SEQUENTIAL_SCAN*/, NULL);

  if (resourceHandle == INVALID_HANDLE_VALUE)
  {
    OutputDebugString(L"Invalid resource\n");
    return;
  }

  CHAR header[ResourceHeaderLength];
  SecureZeroMemory(header, ResourceHeaderLength);

  DWORD bytesRead;
  if (!ReadFile(resourceHandle, static_cast<LPVOID>(header), ResourceHeaderLength, &bytesRead, NULL) || bytesRead <= 0)
  {
    OutputDebugString(L"Error reading resource header\n");
    CloseHandle(resourceHandle);
    return;
  }

  LPCSTR dataStart = strstr(header, "\n");
  if (dataStart == NULL)
  {
    OutputDebugString(L"Error processing resource header\n");
    CloseHandle(resourceHandle);
    return;
  }
  INT dataStartIndex = (dataStart - header) + 1;

  LPSTR headerPropertyContext(NULL);
  LPSTR headerProperty = strtok_s(header, ",", &headerPropertyContext);
  while (headerProperty != NULL)
  {
    if (strstr(headerProperty, "itemSize") != NULL)
    {
      LPSTR itemSizeContext(NULL);
      LPSTR itemSizeKey = strtok_s(headerProperty, ":", &itemSizeContext);
      LPSTR itemSizeValue = strtok_s(NULL, ":", &itemSizeContext);
      OutputDebugString(L"itemSize: ");
      OutputDebugStringA(itemSizeValue);
      OutputDebugString(L"\n");

      if (strstr(itemSizeValue, "-1") != NULL)
      {
        OutputDebugString(L"itemSize not set, data reads will be slower\n");
      }
    }

    headerProperty = strtok_s(NULL, ",", &headerPropertyContext);
  }

  CloseHandle(resourceHandle);
}