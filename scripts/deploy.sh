#!/bin/bash
echo "Using logged in user: $USER"
for i in $(cat machines.txt); do
    host=$i
    echo -e "\e[93m$host\e[0m"
    cat deployLocal.sh | ssh ${USER}@${host}
done