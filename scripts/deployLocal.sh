#!/bin/bash
sourceDir=~/MP1
buildDir=$sourceDir/SkyNet20/SkyNet20
outDir=~/SkyNet
logDir=$outDir/log

cd "$sourceDir"
git pull

if [ ! -d $outDir ]; then
	mkdir $outDir
fi

if [ ! -d $logDir ]; then
	mkdir $logDir
fi

# Terminate existing process
PID=`pgrep "dotnet"`

if [[ "" != $PID ]]
then
	echo "Stopping server..."
	kill -9 $PID
fi

# Build
cd $buildDir
dotnet build -o $outDir

# Start new process
echo "Starting server..."
cd $outDir
dotnet SkyNet20.dll &