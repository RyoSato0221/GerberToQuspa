using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GerberToQuspa
{
    /// <summary>
    /// XMLファイルの読み込み、書き込みを実行するクラス
    /// </summary>
    public class CSettingXMLService<T> where T : new()
    {
        public static bool IsExsitXmlSettingFile(string FilePath)
        {
            return File.Exists(FilePath);
        }
        /// <summary>
        /// XML からデータを読み込み
        /// </summary>
        public static T LoadFromXml(string filePath)
        {
            if (!File.Exists(filePath))
                return new T(); // ファイルがない場合、デフォルトのオブジェクトを返す

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StreamReader reader = new StreamReader(filePath))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
        /// <summary>
        /// XMLファイルの読み込みを実行する
        /// </summary>
        public static T Load(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    return (T)serializer.Deserialize(stream);
                }
                // 意図しない初期化が発生した場合でも気づけるように、
                // ファイルが存在するが、フォーマットエラーの場合は例外で止まるように変更。
            }
            else
            {
                // デフォルトオブジェクト
                T old = new T();
                // 新フォーマットで一旦保存
                SaveToXml(FilePath, old);
                return old;
            }
        }

        /// <summary>
        /// XMLファイルの書き込みを実行する
        /// </summary>
        public static void SaveToXml(string FilePath, T saveInstance)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StreamWriter sw = new StreamWriter(FilePath, false, new UTF8Encoding(false)))
            {
                serializer.Serialize(sw, saveInstance);
            }
        }
    }
}
