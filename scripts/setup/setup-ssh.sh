#!/bin/bash
# Run on host: ssh-keygen -t rsa

for i in $(cat ../machines.txt); do
    host=$i
    echo -e "\e[93m$host\e[0m"
    ssh-copy-id ${USER}@${host}
done