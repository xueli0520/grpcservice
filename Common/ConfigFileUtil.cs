using System.Text;
using System.Text.RegularExpressions;

namespace GrpcService.Common
{
    class ConfigFileUtil
    {
        /**
         * 获取请求数据报文内容
         * @param templateFilePath 报文模板格式文件位置,位于resources文件夹下面（\\conf\\--\\--.xx）
         * @param parameter 模板中可以替换的占位参数信息
         * @return
         */
        public static string GetReqBodyFromTemplate(string templateFilePath, Dictionary<string, object> parameter)
        {
            string templateContent = ReadFileContent(templateFilePath);
            return Replace(templateContent, parameter);
        }

        /**
        * 读取报文配置文件
        *
        * @param filePath 文件相对于resources文件夹的相对路径
        * @return
        */
        public static string ReadFileContent(string filePath)
        {
            string resourcePath = GetResFileAbsPath(filePath);

            // 读取指定文件路径的文件内容
            string contentStr = "";
            try
            {
                contentStr = File.ReadAllText(resourcePath, Encoding.UTF8);
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
            return contentStr;
        }

        /**
         * 替换占位符变量，固定为 ${} 格式
         *
         * @param source    源内容
         * @param parameter 占位符参数
         * @return 替换后的字符串
         */
        public static string Replace(string source, Dictionary<string, object> parameter)
        {
            return Replace(source, parameter, "${", "}");
        }

        public static string Replace(string source, IDictionary<string, object> parameter, string prefix, string suffix)
        {
            var pattern = Regex.Escape(prefix) + "(?<var>.*?)" + Regex.Escape(suffix);
            var regex = new Regex(pattern, RegexOptions.Compiled);
            return regex.Replace(source, m =>
            {
                var variableName = m.Groups["var"].Value;
                if (parameter.TryGetValue(variableName, out object value))
                {
                    return value.ToString();
                }
                return m.Value;
            });
        }

        /**
        * 获取resource文件夹下的文件绝对路径
        *
        * @param filePath 文件相对于resources文件夹的相对路径, 格式描述举例为 \\conf\\XX\\XX.json
        * @return
        */
        public static string GetResFileAbsPath(string filePath)
        {
            return filePath == null
                ? throw new ArgumentNullException("filePath null error!")
                : AppDomain.CurrentDomain.BaseDirectory + "resources" + filePath;
        }
    }
}
