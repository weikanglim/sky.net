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
	if [ -f $logDir ]; then
		rm $logDir
	fi

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
nohup dotnet SkyNet20.dll > skynet.out 2> skynet.err < /dev/null  &
