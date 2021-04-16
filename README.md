### Running Autobahn WebSockets Testsuite for .NET WebSockets

1. Install Docker from https://www.docker.com/get-started
2. Clone https://github.com/crossbario/autobahn-testsuite
3. Open PowerShell in /docker folder from the cloned repository
4. Run `docker run -it --rm -v ${PWD}/config:/config -v ${PWD}/reports:/reports -p 9001:9001 --name fuzzingserver crossbario/autobahn-testsuite`
5. Build the project in this repository in release
6. Clone https://github.com/zlatanov/runtime and checkout `websocket-deflate-v2` branch
7. In the runtime repository run the following command `build.cmd clr+libs -rc Release`
8. After the runtime repo is built, go to `artifacts\bin\testhost\net6.0-windows-Debug-x64\shared\Microsoft.NETCore.App\6.0.0` directory. Open command line there 
and run the following: `CoreRun.exe "path\to\bin\release\net5.0\WebSocketCompliance.dll"`
9. After the tests complete, go into the cloned autobahn-testsuite directory, where in step `4.` we ran a docker command. You will find `reports/client` directory
where you need to open index.html to see the results from the test run.

If you wish to run only some of the test cases you can specify them as arguments like this: 
`CoreRun.exe "path\to\bin\release\net5.0\WebSocketCompliance.dll" 1 50-100`
