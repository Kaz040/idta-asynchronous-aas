#!/bin/sh
dotnet AasxServerBlazor.dll --async --no-security --data-path ./aasxs --external-blazor http://localhost:5001
