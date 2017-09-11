#!/bin/bash
echo "Using logged in user: $USER"
for i in $(cat machines.txt); do
    host=$i
    echo -e "\e[93m$host\e[0m"
    ssh ${USER}@${host} "cd ~/MP1 && git pull"
done