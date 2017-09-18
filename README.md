# Readme

## Running SkyNet.2.0 for grading

To build from source, run the following command in the root project directory:

    build

Note: Since the server and dgrep client are both stored in the same assembly, this will restart the existing server instance.

To test out distributed grep for MP1, first navigate to a directory where you want the grep output files stored, then run the following:

    dotnet ~/SkyNet/SkyNet20.dll -i

When prompted to query log files, simply type in a grep expression and the results will be returned.

For example, typing in the expression "GET",
     
    Query log files: GET
    Results in 4477 ms.
    fa17-cs425-g50-04.cs.illinois.edu : 346832
    fa17-cs425-g50-09.cs.illinois.edu : 350910
    fa17-cs425-g50-03.cs.illinois.edu : 349082
    fa17-cs425-g50-10.cs.illinois.edu : 311317
    fa17-cs425-g50-06.cs.illinois.edu : 351121
    fa17-cs425-g50-08.cs.illinois.edu : 352505
    fa17-cs425-g50-07.cs.illinois.edu : 353362
    fa17-cs425-g50-05.cs.illinois.edu : 348534
    fa17-cs425-g50-02.cs.illinois.edu : 346454
    fa17-cs425-g50-01.cs.illinois.edu : 343341
    Total: 3453458


In the event that no servers have responded, SkyNet.2.0 might not be running on that machine.

To start, or restart SkyNet.2.0 servers on all machines, run the following command in the root project directory:

    deploy
     
