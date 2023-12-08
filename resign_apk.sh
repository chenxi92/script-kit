#!/bin/bash
#
# Resign apk file
# 
# Find the ipa file at the current directory or
# you can specify the location as the first parameter
# 
# Usage:
# sh resign_apk.sh <apk_file_path>
# sh resign_apk.sh


set -e # exit immediately
set -u # uninitialized variables mode
# set -x # debug model

# Find apk at current folder if not specified.
apk_path=${1:-$(find $(pwd) -maxdepth 1 -name *.apk)}
apk_decompile_folder="./tmp"

apk_full_name=$(basename ${apk_path})
apk_source_path="${apk_decompile_folder}/dist/${apk_full_name}"
apk_output_path="${apk_decompile_folder}/dist/${apk_full_name%.*}_resigned.apk"

# Use the key store file generated by Android Studio automatically
key_store_path="$HOME/.android/debug.keystore"
key_store_alias="androiddebugkey"
key_store_password="android"


function log() {
	echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*"
}

function decompile_apk() {
	log "start decompile apk: ${apk_path}"
	if [[ -d "${apk_decompile_folder}" ]]; then
		log "delete existed folder: ${apk_decompile_folder}"
		rm -rf "${apk_decompile_folder}"
	fi
	apktool d -f -o "${apk_decompile_folder}" "${apk_path}"
	# -s: keep classes.dex, default will convert dex file to smail file
	# -f: force rewrite
	# -o: output 
}

function modify_apk() {
	log "modify apk at: ${apk_decompile_folder}"
	# custom implementation
}

function recompile_apk() {
	log "start to recompile apk"
	if [[ -d "${apk_decompile_folder}" ]]; then
		log "build modified apk at: ${apk_decompile_folder}"
		apktool b "${apk_decompile_folder}"
	fi
}

function sign_apk() {
	log "start to re-sign apk"
	if [[ -f "${apk_output_path}" ]]; then
		log "delete existed apk output path: ${apk_output_path}"
		rm -rf "${apk_output_path}"
	fi
	
	build_tools="${HOME}/Library/Android/sdk/build-tools"
	apksigner="$(ls -d ${build_tools}/* | tail -n 1)/apksigner"
	if [[ ! -f "${apksigner}" ]]; then
		log "not exist apksigner at: ${apksigner}"
		log "check out the offocial document: https://developer.android.com/tools/apksigner"
		exit 1
	fi
	# ${apksigner} sign --help

	$apksigner sign\
	--ks ${key_store_path} \
	--ks-key-alias ${key_store_alias} \
	--ks-pass pass:${key_store_password}\
	--in  ${apk_source_path} \
	--out ${apk_output_path}

	log "finfish re-sign apk"
}

function install_apk() {
	log "start install apk: ${apk_output_path}"
	if [[ -f ${apk_output_path} ]]; then
		adb install ${apk_output_path}
	fi
	log "finished install apk"
}

function main() {
	decompile_apk
	modify_apk
	recompile_apk
	sign_apk
	install_apk
}

main
