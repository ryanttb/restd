#include "stdafx.h"

#include "sandbox.h"
INT _tmain(INT argc, _TCHAR* argv[])
{
#ifdef _DEBUG
  DumpResource(L"empty.restd");
#endif

	return 0;
}

