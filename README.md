# weilim - Sky.Net (CS 425)
## Readme

Sky.Net is a distributed system implemented in .NET Core that supports:
  1. A distributed file system
  2. Distributed jobs processing such as distributed PageRank

Sky.Net is resilient up to 4 node failures at a given time.

## Running Sky.Net.2.0 for grading

To build and run from source, run the following command in the root project directory:

    cd <root directory>
    ./build

This will build the project and launch the SkyNet program.

Follow instructions from the interactive console to perform tasks. You should see the following prompt showing up in your console:
    
    List of commands:
    [1] Show membership list
    [2] Show machine id
    [3] Join the group
    [4] Leave the group
    [5] put <localfilename> <sdfsfilename>
    [6] get <sdfsfilename> <localfilename>
    [7] delete <sdfsfilename>
    [8] ls <sdfsfilename>
    [9] store
    [10] Submit sava job