#!/bin/bash
echo "Using logged in user: $USER"
pushd $(dirname "$0")

if [ -f "$1" ]
then
    script="$1"
else
    cmd="$1"
fi

for i in $(cat machines.txt); do
    host=$i
    echo -e "\e[93m$host\e[0m"
    if [[ $cmd ]]
    then
        ssh -t -t ${USER}@${host} "$cmd"
    else
        cat "$script" | ssh -T ${USER}@${host}
    fi
done

popd