using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CopyQuery
{
    [DataContract]
    public class YouDaoTranslationResponse
    {
        [DataMember(Name = "errorCode")]
        public string ErrorCode { get; set; }

        [DataMember(Name = "query")]
        public string QueryText { get; set; }

        [DataMember(Name = "speakUrl")]
        public string InputSpeakUrl { get; set; }

        [DataMember(Name = "tSpeakUrl")]
        public string TranslationSpeakUrl { get; set; }

        /// <summary>
        /// 首选翻译
        /// </summary>
        [DataMember(Name = "translation")]
        public List<string> FirstTranslation { get; set; }

        /// <summary>
        /// 基本释义
        /// </summary>
        [DataMember(Name = "basic")]
        public TranslationBasicData BasicTranslation { get; set; }

        ///// <summary>
        ///// 网络释义，该结果不一定存在,暂时不使用
        ///// </summary>
        //[DataMember(Name = "web")]
        //public TranslationWebData WebTranslation { get; set; }
    }

    /// <summary>
    /// 基本释义
    /// </summary>
    [DataContract]
    public class TranslationBasicData
    {
        [DataMember(Name = "phonetic")]
        public string Phonetic { get; set; }

        /// <summary>
        /// 英式发音
        /// </summary>
        [DataMember(Name = "uk-phonetic")]
        public string UkPhonetic { get; set; }

        /// <summary>
        /// 美式发音
        /// </summary>
        [DataMember(Name = "us-phonetic")]
        public string UsPhonetic { get; set; }

        /// <summary>
        /// 翻译
        /// </summary>
        [DataMember(Name = "explains")]
        public List<string> Explains { get; set; }
    }

    /// <summary>
    /// 网络释义
    /// </summary>
    [DataContract]
    public class TranslationWebData
    {
        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "value")]
        public List<string> Explains { get; set; }
    }
    /// <summary>
    /// 有道词典API
    /// </summary>
    internal class YouDaoApiService
    {
        const string AppKey = "131b76a4ee1ecd13";//AppKey和AppSecret是本人@Winter申请的账号，仅供测试使用
        const string LangEn = "en";
        const string AppSecret = "KX9hLrgSMhfKkvIqS6nhwtwMcRymJqEA";

        public static async Task<YouDaoTranslationResponse> GetTranslatioAsync(string queryText, string from = LangEn, string to = LangEn)
        {
            var requestUrl = GetRequestUrl(queryText, from, to);

            WebRequest translationWebRequest = WebRequest.Create(requestUrl);

            var response = await translationWebRequest.GetResponseAsync();

            using (Stream stream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException("有道Api查询出错！"), Encoding.GetEncoding("utf-8")))
                {
                    string result = reader.ReadToEnd();
                    var youDaoTranslationResponse = JsonConvert.DeserializeObject<YouDaoTranslationResponse>(result);

                    return youDaoTranslationResponse;
                }
            }
        }

        private static string GetRequestUrl(string queryText, string from, string to)
        {
            string salt = DateTime.Now.Millisecond.ToString();

            MD5 md5 = new MD5CryptoServiceProvider();
            string md5Str = AppKey + queryText + salt + AppSecret;
            byte[] output = md5.ComputeHash(Encoding.UTF8.GetBytes(md5Str));
            string sign = BitConverter.ToString(output).Replace("-", "");

            var requestUrl = string.Format(
                "http://openapi.youdao.com/api?appKey={0}&q={1}&from={2}&to={3}&sign={4}&salt={5}",
                AppKey,
                HttpUtility.UrlDecode(queryText, System.Text.Encoding.GetEncoding("UTF-8")),
                from, to, sign, salt);

            return requestUrl;
        }
    }

}