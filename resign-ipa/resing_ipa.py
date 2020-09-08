#! utf-8
import argparse
import subprocess
import os
import getpass
import time
import plistlib
import shutil
import random


SIGN_FILE_SUFFIX = [".dylib", ".so", ".0", ".vis",
                    ".pvr", ".framework", ".appex", ".app"]
WS_FILES = []
SEQ = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"


def generateFilePath(app_file_path):
    nameArray = []
    for index in xrange(1, random.randint(5, 30)):
        nameArray.append(random.choice(SEQ))
    name = ''.join(nameArray)
    file_path = os.path.join(app_file_path, name)
    if not os.path.exists(file_path):
        return file_path
    else:
        generateFilePath(app_file_path)


def randomContent(file_path):
    with open(file_path, 'wr+') as fp:
        s = []
        minIndex = random.randint(512, 1024)
        maxIndex = random.randint(1024, 2048)
        for x in xrange(1, minIndex * maxIndex):
            s.append(random.choice(SEQ))
        fp.write(''.join(s))


def generateGarbageResource(app_file_path, size=0):
    '''Generate garbage resource, default size is 0, if size is greater than 0, that will generate garbadge resource'''
    if size == 0:
        return
    print "total garbage resource size = %.2f MB" % size
    file_size = 0
    while file_size < size:
        file_path = generateFilePath(app_file_path)
        randomContent(file_path)
        _size = float(os.path.getsize(file_path)) / 1024 / 1024
        print "file = %s \nsize = %.2f MB\n" % (file_path, _size)
        file_size += _size
    print "file total size = %.2f MB" % file_size


def executeCommand(cmd):
    s = subprocess.Popen(cmd, stdin=subprocess.PIPE,
                         stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
    stdoutput, erroutput = s.communicate()
    ret = s.wait()
    if ret:
        raise Exception(
            "execute command = {} Failed.\nerror = {}".format(cmd, erroutput))
    return stdoutput


def handleInfoPlist(info_plist_path, bundle_identity=None, version=None, displayName=None):
    '''
    change the vlue in Info.plist file from the given value
    '''
    if not os.path.exists(info_plist_path):
        return
    dic = {}
    if bundle_identity is not None:
        dic['CFBundleIdentifier'] = bundle_identity
    if version is not None:
        # keep short version and version the same, not necessary
        dic['CFBundleShortVersionString'] = version
        dic['CFBundleVersion'] = version
    for key, value in dic.items():
        command = "/usr/libexec/PlistBuddy -c 'Set :" + key + \
            " " + value + "' " + '"' + info_plist_path + '"'
        executeCommand(command)


def recursiveFiles(path):
    for file in os.listdir(path):
        file_path = os.path.join(path, file)
        if os.path.splitext(file)[1] in SIGN_FILE_SUFFIX:
            WS_FILES.append(file_path)
        # Delete the code resources file
        if os.path.split(file_path)[1] == 'CodeResources':
            _CodeResources = os.path.split(file_path)[0]
            command = "rm -r '" + _CodeResources + "'"
            executeCommand(command)
            if os.path.exists(_CodeResources):
                raise Exception('remove _CodeResources dir fail')
        if os.path.isdir(file_path):
            recursiveFiles(file_path)


def handleEntitlementPlist(profile_path, app_file_path):
    '''
    generate the plist file, containt the `Entitlements` filed
    if success, return the plist path and bundle identity
    '''

    # 1. export the profile
    temp_file_path = os.path.join(os.path.dirname(app_file_path), "temp.plist")
    if os.path.exists(temp_file_path):
        os.remove(temp_file_path)
    # with open(temp_file_path, 'wr') as fp:
    #     pass
    command = "security cms -D -i '" + profile_path + "' > " + temp_file_path
    executeCommand(command)

    # 2. export the `temp_entitlement_file` fileds
    temp_entitlement_file = os.path.join(
        os.path.dirname(app_file_path), "Entitlements.plist")
    if os.path.exists(temp_entitlement_file):
        os.remove(temp_entitlement_file)
    command = "/usr/libexec/PlistBuddy -x -c 'Print:Entitlements' '" + \
        temp_file_path + "'>" + temp_entitlement_file
    executeCommand(command)

    os.remove(temp_file_path)

    # 3. get identifier
    plistData = plistlib.readPlist(temp_entitlement_file)
    application_identifier = plistData['application-identifier']
    team_identifier = plistData['com.apple.developer.team-identifier'] + "."
    identifier = application_identifier.replace(team_identifier, "")
    return temp_entitlement_file, identifier


def getProfilePath(profile_name):
    '''
    get profile path, always return the newest profile path
    '''
    provision_profile_dir = "/Users/" + \
        getpass.getuser() + "/Library/MobileDevice/Provisioning Profiles"
    profile_data = {}
    for file in os.listdir(provision_profile_dir):
        if os.path.splitext(file)[1] == '.mobileprovision':
            file_path = os.path.join(provision_profile_dir, file)
            command = '/usr/bin/security cms -D -i ' + "'" + file_path + "'"
            plistStr = executeCommand(command)
            plist = plistlib.readPlistFromString(plistStr)
            expiration_time = time.mktime(plist["ExpirationDate"].timetuple())
            if (expiration_time < time.time()):  # if the profile was expirated, should delete
                print "file in [%s] was expirated(%s), now remove." % (file_path, plist['ExpirationDate'])
                os.remove(file_path)
                continue
            if plist['Name'] == profile_name:
                profile_path = os.path.join(
                    provision_profile_dir, plist['UUID'] + '.mobileprovision')
                profile_data[expiration_time] = profile_path

    if len(profile_data.keys()) < 1:
        raise Exception("can't find a valid profile file")
    else:
        all_keys = []
        for key in profile_data:
            all_keys.append(key)
        # compare the expiration time, the max data, means the news profile
        max_key = max(all_keys)
        return profile_data[max_key]


def unpack(file_path):
    # first delete Payload folder if exist
    out_dir = os.path.join(os.path.dirname(file_path), "Payload")
    if os.path.exists(out_dir):
        shutil.rmtree(out_dir)
    # unzip the file
    command = "unzip -q \'" + file_path + "\'"
    executeCommand(command)
    for file in os.listdir(out_dir):
        path = os.path.join(out_dir, file)
        if path.endswith(".app"):
            return path
    raise Exception("can't find app file")


def pack(payload_dir, output_file_path):
    os.chdir(payload_dir)
    command = "zip -qyr \'" + output_file_path + "\' Payload"
    executeCommand(command)


def resign(profile_name, certificate_name, input_file_path, output_file_path, version=None, garbageSize=0):
    '''
    resign a ipa file.
    profile_name : a .mobileprovision file's UUID
    certificate_name : a certificate name which use to sign the app, must start with  `iPhone Developer:` or `iPhone Distribution:`
    input_file_path : the ipa file path
    output_file_path : the output file path
    version : the ipa build version, optional
    '''
    print "\nprofile name : [%s]\ncertificate name : [%s]\nbuild version : [%s]\nipa file path : [%s]\noutput file path : [%s]\n" % (profile_name, certificate_name, version, input_file_path, output_file_path)
    if os.path.exists(output_file_path):
        os.remove(output_file_path)
    print "begin check profile ..."

    profile_path = getProfilePath(profile_name)
    print "profile path = %s\n" % profile_path

    app_path = unpack(input_file_path)
    embedded_profile_path = os.path.join(app_path, "embedded.mobileprovision")
    print "app path = %s" % app_path
    if os.path.exists(embedded_profile_path):
        os.remove(embedded_profile_path)
    shutil.copyfile(profile_path, embedded_profile_path)

    generateGarbageResource(app_path, garbageSize)

    entitlement_plist_path, bundle_identity = handleEntitlementPlist(
        profile_path, app_path)
    print "\nentitlement_plist_path = {}\nbundle_identity = {}".format(entitlement_plist_path, bundle_identity)

    info_plist_path = os.path.join(app_path, 'Info.plist')
    handleInfoPlist(info_plist_path, bundle_identity, version)

    # recursive file and sign the files
    recursiveFiles(app_path)
    for file in WS_FILES:
        command = '/usr/bin/codesign -vvv -fs ' + '"' + certificate_name + '"' + \
            ' --no-strict --entitlements "' + entitlement_plist_path + '" ' + file
        print "resing file : %s" % command
        executeCommand(command)

    # sign the .app
    command = '/usr/bin/codesign -vvv -fs ' + '"' + certificate_name + '"' + \
        ' --no-strict --entitlements "' + entitlement_plist_path + '" ' + app_path
    print "resign app : %s" % command
    executeCommand(command)

    # check resign is success of fail
    command = '/usr/bin/codesign --verify "' + app_path + '"'
    ret = executeCommand(command)
    if ret:
        raise Exception("resign app failed, check result = %s" % ret)

    # delete the temp entitlement file
    os.remove(entitlement_plist_path)

    # pack to ipa
    payload_dir = os.path.abspath(
        os.path.join(os.path.dirname(app_path), "../"))
    payload_path = os.path.join(payload_dir, "Payload")
    pack(payload_dir, output_file_path)
    shutil.rmtree(payload_path)


def main():
    parser = argparse.ArgumentParser(
        description='Resign an ipa file, with a .mobileprovision files name, and use a certificate')
    parser.add_argument('-in', '--input', help='Path to input .ipa file')
    parser.add_argument(
        '-out', '--output', help='Path to output .ipa file. Defaults to outputting into the same directory.')
    parser.add_argument('-p', '--profile',
                        help='a .mobileprovision files UUID', required=True)
    parser.add_argument('-c', '--certificate',
                        help="a certification's name", required=True)
    parser.add_argument('-v', '--buildVersion', help="the build version")
    parser.add_argument('-s', '--garbageSize',
                        help="generate garbage resources size")
    args = parser.parse_args()
    if len(args.profile) < 1:
        raise Exception("mobileprovision file name must not be nil")

    if not args.certificate.startswith('iPhone'):
        raise Exception(
            "certification name {%s} not correct" % args.certificate)

    if not os.path.isabs(args.input):
        args.input = os.path.join(os.path.abspath("."), args.input)
    if not os.path.exists(args.input):
        raise Exception(
            "input file {%s} not exist, please check again." % args.input)

    if args.output is None:
        head, tail = os.path.split(args.input)
        if args.buildVersion is None:
            outputFileName = tail.split(".ipa")[0] + "_output_.ipa"
        else:
            outputFileName = tail.split(
                ".ipa")[0] + "_" + args.buildVersion + "_output_.ipa"
        args.output = os.path.join(head, outputFileName)
    else:
        if not os.path.isabs(args.output):
            args.output = os.path.join(os.path.abspath("."), args.output)

    if args.garbageSize is None:
        args.garbageSize = 0
    resign(args.profile, args.certificate, args.input,
           args.output, args.buildVersion, int(args.garbageSize))


if __name__ == '__main__':
    begin = time.time()
    main()
    end = time.time()
    print("cost time : %.2fs" % (end - begin))
