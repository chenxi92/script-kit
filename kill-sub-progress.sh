# /bin/bash

set -eu

if [[ $# -lt 1 ]]; then
	echo "usage: sh kill-sub-progress.sh <port>"
	exit 1
fi

port=$1
if [[ $(lsof -i :"${port}" | wc -l) -lt 2 ]]; then
	echo "port ${port} not exist active progress"
	exit 1
fi

pid=$(lsof -i :"${port}" | head -n 2 | tail -n 1 | awk '{print $2}')
if [[ ! -z ${pid} ]]; then
	echo "port ${port} find PID: ${pid}"
	kill -9 ${pid}
fi

echo "port ${port} not find PID"