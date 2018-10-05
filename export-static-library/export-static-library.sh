#!/bin/bash

function msgShow() {
    echo -e "\033[32m$1\033[0m"
}

function errShow() {
    echo -e "\033[31m$1\033[0m"
}

function checkIsValid() {
    # split with `:`, 
    VALID_ARCHS=`lipo -info $1 | awk -F : '{print $3}'`
    # get the result numbers
    VALID_ARCHS_NUMS=`echo $VALID_ARCHS | awk '{print NF}'`
    if [[ $VALID_ARCHS_NUMS == 4 ]]; then
        msgShow "check archs OK"
    else
        msgShow "当前只支持如下架构:"
        echo $VALID_ARCHS
        errShow "请检查Xcode配置, Build Settings -> iOS Deployment Target，选择稍低的iOS版本"
        exit 
    fi
}


if test -d build
	then rm -rf build
fi

xcodebuild clean

xcodebuild ONLY_ACTIVE_ARCH=NO IPHONEOS_DEPLOYMENT_TARGET=8.0 -configuration Release -sdk iphoneos 
xcodebuild ONLY_ACTIVE_ARCH=NO IPHONEOS_DEPLOYMENT_TARGET=8.0 -configuration Release -sdk iphonesimulator

path=""
for file in "./build/Release-iphoneos/**.a"
do
	if test -f $file
	then
		path=$file
	fi
done

pluginName=$(basename $path)
if test -f ${pluginName}
then 
	rm -r ${pluginName}
fi

# 合并真机和模拟器版本
lipo -create build/Release-iphoneos/**.a build/Release-iphonesimulator/**.a -output ./libs/${pluginName}


if [[ ! -f ./libs/${pluginName} ]]; then
    msgShow "文件不存在"
    exit 1
fi

# 检查是否支持所有架构
checkIsValid ./libs/${pluginName}

# 删除build目录
rm -rf build

msgShow "done ..."

