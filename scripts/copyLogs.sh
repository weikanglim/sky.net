#!/bin/bash
echo "Using logged in user: $USER"

if ! [ -d "$1" ]
then
    echo "Log directory $1 does not exist."
else
    logDir="$1"
fi

counter=1
for i in $(cat machines.txt); do
    host=$i
    file=$logDir/vm.$counter.log
    echo -e "\e[93m$host\e[0m"

    if [[ -f $file ]]
    then 
        echo "Copying $file"
        scp $file ${USER}@${host}:~/SkyNet/log
    else
        echo "$file does not exist."
    fi

    counter=$[$counter+1]
done
