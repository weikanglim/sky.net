#!/bin/bash

for i in "$1/vm*.log"; do
    grep $3 "$1/$i" > "$2/$i"
done