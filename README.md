#Dotnet_DumpHightCpuCallStack

the reference dlls Microsoft.Samples.Debugging.* are from the below source code:
https://github.com/SymbolSource/Microsoft.Samples.Debugging  

the idea and code changed from:
http://www.cnblogs.com/onlytiancai/archive/2009/06/24/heightcpu_diag.html

if you use the tool in x64 platform, please build it with target platform x64.


#Function
When your dotnet app(run on windows) use hight cpu and you don't know how, 
this tool can help you quickly find out the issue by dump the callstack.
this tool create a PerformanceCounter and dump the hight cpu level you specified callstack,
you can analyze the callstack and quickly find out the issue code.
