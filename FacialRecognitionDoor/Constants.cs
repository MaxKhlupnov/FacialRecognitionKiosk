using FacialRecognitionDoor.FacialRecognition;

namespace FacialRecognitionDoor
{
    /// <summary>
    /// General constant variables
    /// </summary>
    public static class GeneralConstants
    {
        // This variable should be set to false for devices, unlike the Raspberry Pi, that have GPU support
        public const bool DisableLiveCameraFeed = false;

        // Oxford Face API Primary should be entered here
        // You can obtain a subscription key for Face API by following the instructions here: https://www.projectoxford.ai/doc/general/subscription-key-mgmt
        public const string OxfordAPIKey = "please proivde your API key here";

        // Name of the folder in which all Whitelist data is stored
        public const string WhiteListFolderName = "Facial Recognition Door Whitelist";

    }

    /// <summary>
    /// Constant variables that hold messages to be read via the SpeechHelper class
    /// </summary>
    public static class SpeechContants
    {
        /*   public const string InitialGreetingMessage = "Welcome to the Facial Recognition Door! Speech has been initialized.";

           public const string VisitorNotRecognizedMessage = "Sorry! I don't recognize you, so I cannot open the door.";
          public const string NoCameraMessage = "Sorry! It seems like your camera has not been fully initialized.";*/
        public const string InitialGreetingMessage = "Добрый день, добро пожаловать в центр технологий Майкрософт! Голосовые функции инициализированы успешно.";
        public const string VisitorNotRecognizedMessage = "Извините, я не могу Вас идентифицировать. Поэтому я не могу предоставить Вам доступ.";
        public const string VisitorsCountMessage = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
                             xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' 
                             xsi:schemaLocation='http://www.w3.org/2001/10/synthesis  http://www.w3.org/TR/speech-synthesis/synthesis.xsd' 
                             xml:lang='ru-RU'>                               
                               <p>Число распознанных лиц: <say-as interpret-as='cardinal'>{0}</say-as></p>                               
                            </speak>";
        public const string UnknownVisitorDetailMessage = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
                             xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' 
                             xsi:schemaLocation='http://www.w3.org/2001/10/synthesis  http://www.w3.org/TR/speech-synthesis/synthesis.xsd' 
                             xml:lang='ru-RU'>                               
                               <p>Вы идентифицированы как {0}. Ваш примерный возраст <say-as interpret-as='cardinal'>{1}</say-as> лет.</p> 
                                <p>Добро пожаловать на форум!</p> 
                            </speak>";
        public const string NoCameraMessage = "Извините! Кажется Ваша видео-камера не может быть инициализирована.";
        public const string NoFaceDetectedMessage = "Лица не распознаны";

        public static string GeneralGreetigMessage(string visitorName)
        {
            // return "Здравствуйте, " + visitorName + "! Добро пожаловать на мероприятие в \"Технологическом центре Майкрософт\"! Пакет участника ожидает Вас на рецепшен.";
            return "Здравствуйте, " + visitorName + "! Добро пожаловать в центр технологий Майкрософт!";
            // return "Welcome to the Facial Recognition Door " + visitorName + "! I will open the door for you.";
        }
       
    }

    /// <summary>
    /// Constant variables that hold values used to interact with device Gpio
    /// </summary>
    public static class GpioConstants
    {
        // The GPIO pin that the PirSensor is attached to
        public const int PirSensorPinID = 9;

        // The GPIO pin that the doorbell button is attached to
        public const int ButtonPinID = 8;

        // The GPIO pin that the door lock is attached to
        public const int DoorLockPinID = 4;

        // The amount of time in seconds that the door will remain unlocked for
        public const int DoorLockOpenDurationSeconds = 10;
    }
}
