using FacialRecognitionDoor.Helpers;

namespace FacialRecognitionDoor.Objects
{
    /// <summary>
    /// Object specifically to be passed to UserProfilePage that contains an instance of the WebcamHelper and a Member object
    /// </summary>
    class UserProfileObject
    {
        /// <summary>
        /// An initialized Member object
        /// </summary>
        public Member Member { get; set; }

        /// <summary>
        /// An initialized WebcamHelper 
        /// </summary>
        public WebcamHelper WebcamHelper { get; set; }

        /// <summary>
        /// Initializes a new UserProfileObject with relevant information
        /// </summary>
        public UserProfileObject(Member visitor, WebcamHelper webcamHelper)
        {
            Member = visitor;
            WebcamHelper = webcamHelper;
        }
    }
}
