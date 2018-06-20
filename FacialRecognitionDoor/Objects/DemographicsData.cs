using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using System.Text;

namespace FacialRecognitionDoor.Objects
{

    [XmlType]
    public class Visitor
    {
        [XmlAttribute]
        public Guid UniqueId { get; set; }

        [XmlAttribute]
        public int Count { get; set; }
    }

    [XmlType]
    public class AgeDistribution
    {
        public int Age0To15 { get; set; }
        public int Age16To19 { get; set; }
        public int Age20s { get; set; }
        public int Age30s { get; set; }
        public int Age40s { get; set; }
        public int Age50sAndOlder { get; set; }
    }

    [XmlType]
    public class AgeGenderDistribution
    {
        public AgeDistribution MaleDistribution { get; set; }
        public AgeDistribution FemaleDistribution { get; set; }
    }

    [XmlType]
    [XmlRoot]
    public class DemographicsData
    {
        public DateTime StartTime { get; set; }

        public AgeGenderDistribution AgeGenderDistribution { get; set; }

        public int OverallMaleCount { get; set; }

        public int OverallFemaleCount { get; set; }

        [XmlArrayItem]
        public List<Visitor> Visitors { get; set; }

        public static async void Save(DemographicsData data)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(DemographicsData));
            StorageFile file = await ApplicationData.Current.RoamingFolder.CreateFileAsync("DemographicsData.txt", CreationCollisionOption.ReplaceExisting);
            using (Stream outStream = await file.OpenStreamForWriteAsync())
            {
                using (XmlWriter xmlTextWriter = XmlWriter.Create(outStream))
                {
                    serializer.Serialize(xmlTextWriter, data);
                }
            }
        }

        public static async Task<DemographicsData> Load()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(DemographicsData));
                StorageFile file = await ApplicationData.Current.RoamingFolder.GetFileAsync("DemographicsData.txt");
                using (Stream inStream = await file.OpenStreamForReadAsync())
                {
                    using (XmlReader xmlTextReader = XmlReader.Create(inStream))
                    {
                        return serializer.Deserialize(xmlTextReader) as DemographicsData;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
