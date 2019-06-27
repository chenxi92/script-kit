# coding:utf-8

import os
import sys
import bs4
import csv
import urllib2
import time

reload(sys)
sys.setdefaultencoding('utf-8')


def download_exchange_rate_content(from_currency, to_currency, amount):
    url = "https://qq.ip138.com/hl.asp?from=" + \
        from_currency + "&to=" + to_currency + "&q=" + amount
    print url
    content = ""
    max_times = 10
    for i in range(max_times):
        try:
            content = urllib2.urlopen(url, timeout=30).read()
            break
        except:
            if i < max_times:
                print("try {0} times.").format(i + 1)
                time.sleep(5)
                continue
            else:
                print "time out."
    return content


def extract_exchange_rate_info(content):

    soup = bs4.BeautifulSoup(content)
    tb = soup.find_all('table')[0]
    rate_infos = []
    trs = tb.findAll('tr')
    for tr in trs:
        # sub_arrays = []
        tds = tr.findAll('td')
        for td in tds:
            text = td.getText()
            rate_infos.append(text)
        # if len(sub_arrays) > 0:
        # 	rate_infos.append(sub_arrays)

    return rate_infos


def write_currency_info_into_csv(info, path):
    f = open(path, "a+")
    wr = csv.writer(f)
    wr.writerow(info)
    f.close()


def get_currency_info(url):
    content = urllib2.urlopen(url).read()
    soup = bs4.BeautifulSoup(content)
    dic = {}
    dds = soup.find_all('dd')
    for dd in dds:
        all_links = dd.findAll('a')
        for link in all_links:
            zh_name = link.text[:-3]
            currency = link.text[-3:]
            dic[zh_name] = currency
    return dic


def main():
    # 1. 获取国家以及币种信息
    currency_url = "https://qq.ip138.com/hl.asp"
    currency_info = get_currency_info(currency_url)

    csv_file_path = os.path.join(os.getcwd(), "rate-info.csv")
    if os.path.exists(csv_file_path):
        os.remove(csv_file_path)

    g_index = 1
    for country_name in currency_info:
        code = currency_info[country_name]
        if code != "USD":
            print("\n开始请求{0} 美元 - {1} 汇率信息...").format(g_index, country_name)
            g_index += 1
            # 2. 下载特定汇率页面信息
            content = download_exchange_rate_content("USD", code, "1")
            if content != "":
                # 3. 提取汇率信息
                rate_info = extract_exchange_rate_info(content)
                if len(rate_info) == 6: # 汇率信息提取6项结果
                    # 4. 写入文件
                    rate_info.append(code)
                    rate_info.append("USD")
                    write_currency_info_into_csv(rate_info, csv_file_path)
            print("--- 请求完成 --- ")


if __name__ == "__main__":
    main()
