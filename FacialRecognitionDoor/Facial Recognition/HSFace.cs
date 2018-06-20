using System;
using ClientContract = Microsoft.ProjectOxford.Face.Contract;

namespace FacialRecognitionDoor.FacialRecognition
{
    /// <summary>
    /// Face data structure
    /// </summary>
    class HSFace
    {
        /// <summary>
        /// Face id for Face API
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Image file name of the face belongs to
        /// </summary>
        public string ImageFile { get; set; }

        public double Age
        {
            get
            {
                if (Attributes != null)                     
                   return Attributes.Age;                    
                else
                    return -1.0;
            }
        }

        public string Gender
        {
            get
            {
                if (Attributes != null)
                    if(Attributes.Gender.Equals("male",StringComparison.CurrentCultureIgnoreCase)) 
                        return "мужчина";
                     else
                        return "женщина";
                else
                    return string.Empty;
            }
        }

        public bool Smile
        {
            get
            {
                if (Attributes != null)
                    return (Attributes.Smile > 50.0);
                else
                    return false;
            }
        }

        public HSFace() { }

        public HSFace(Guid id, string imageFile)
        {
            Id          = id;
            ImageFile   = imageFile;
        }

        private ClientContract.FaceAttributes Attributes = null;
        public HSFace(Guid id, string imageFile, ClientContract.FaceAttributes attributes)
        {
            Id = id;
            ImageFile = imageFile;
            Attributes = attributes;
        }
    }
}
