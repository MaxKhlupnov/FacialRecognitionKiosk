using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceHelpers;

namespace FacialRecognitionDoor.Helpers
{
    public class Recognition 
    {
        public Recognition() { }
        public string id { get; set; }
        public DateTime Timestamp { get; set; }        
        public double Age { get; set; }
        public double Anger { get; set; }
        public double Contempt { get; set; }
        public double Disgust { get; set; }
        public double Fear { get; set; }
        public double Happiness { get; set; }
        public double Neutral { get; set; }
        public double Sadness { get; set; }
        public double Surprise { get; set; }
        public string Gender { get; set; }
        public string Glasses { get; set; }
         public string Location { get; set; }
        public string FaceId { get; set; }
        public string PersonName { get; set; }

    }
}
