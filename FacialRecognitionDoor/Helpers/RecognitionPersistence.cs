using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using ServiceHelpers;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Emotion.Contract;
using Windows.System.Threading;
using Microsoft.ProjectOxford.Common.Contract;

namespace FacialRecognitionDoor.Helpers
{
    public class RecognitionPersistence
    {
        private string Location = "MTC1";
        public static MobileServiceClient MobileService = new MobileServiceClient(
            "http://platformams.azurewebsites.net"
        );

        private IMobileServiceTable<Recognition> recognitionTableObj = null;

        private Queue<Recognition> faceRecognitionQueue = new Queue<Recognition>();
        public RecognitionPersistence()
        {
            /*  this.Location = SettingsHelper.Instance.LocationName;
              if (string.IsNullOrEmpty(SettingsHelper.Instance.TableStorageAccountName) || string.IsNullOrEmpty(SettingsHelper.Instance.TableStorageKey))
                  return;

              string connection = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                  SettingsHelper.Instance.TableStorageAccountName, SettingsHelper.Instance.TableStorageKey);

              // Parse the connection string and return a reference to the storage account.
              CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connection);

              // Create the table client.
              this.tableClient = storageAccount.CreateCloudTableClient();

              CloudTable tableRecognition = tableClient.GetTableReference("Recognition");

              // Create the table if it doesn't exist.
              tableRecognition.CreateIfNotExistsAsync();*/
            recognitionTableObj = MobileService.GetTable<Recognition>();
            StartWorker();
        }

        private void StartWorker()
        {
            TimeSpan period = TimeSpan.FromSeconds(60);

            ThreadPoolTimer PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer((source) =>
            {
                Persistence();
                //
                // Update the UI thread by using the UI core dispatcher.
                //
                /*  Dispatcher.RunAsync(CoreDispatcherPriority.High,
                      () =>
                      {
                  //
                  // UI components can be accessed within this scope.
                  //

              });*/

            }, period);
        }

        private async void Persistence()
        {

            /* CloudTable tableRecognition = tableClient.GetTableReference("Recognition");

             // Create the batch operation.
             TableBatchOperation batchOperation = new TableBatchOperation();

             while (faceRecognitionQueue.Count > 0) {
                 batchOperation.Insert(faceRecognitionQueue.Dequeue());
             }
             if (batchOperation.Count > 0)
                 await tableRecognition.ExecuteBatchAsync(batchOperation);
            */
            while (faceRecognitionQueue.Count > 0)
            {
                recognitionTableObj.InsertAsync(faceRecognitionQueue.Dequeue());
            }
           
        }

        public void PersisteRecognitionResults(ImageAnalyzer data)
        { 
            if (recognitionTableObj != null)
                data.FaceDetectionCompleted += Data_FaceDetectionCompleted;
               

        }

        private async void Data_FaceDetectionCompleted(object sender, EventArgs e)
        {
            ImageAnalyzer data = sender as ImageAnalyzer;
            if (data == null)
                return;
            if (data.DetectedEmotion == null)
                await data.DetectEmotionAsync();          

            foreach (Face face in data.DetectedFaces)
            {

                Recognition recognizedFace = new Recognition()
                {                   
                    id = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.Now,
                    Location = this.Location,                   
                    Age = face.FaceAttributes.Age,
                    Gender = face.FaceAttributes.Gender,
                    Glasses = Enum.GetName(typeof(Glasses), face.FaceAttributes.Glasses)
                    
                };
                
                FillFaceEmotions(recognizedFace, face.FaceRectangle, data.DetectedEmotion);

                this.faceRecognitionQueue.Enqueue(recognizedFace);
            }
            

            

        }

        private void FillFaceEmotions(Recognition recognizedFace, FaceRectangle rectangle,
            IEnumerable<Microsoft.ProjectOxford.Common.Contract.Emotion> detectedEmotion)
        {
            if (detectedEmotion == null)
                return;

            Microsoft.ProjectOxford.Common.Contract.Emotion emotion =
                detectedEmotion.FirstOrDefault<Microsoft.ProjectOxford.Common.Contract.Emotion>(em => em.FaceRectangle.Left == rectangle.Left &&
                em.FaceRectangle.Top == rectangle.Top 
                && em.FaceRectangle.Width == rectangle.Width 
                && em.FaceRectangle.Height == rectangle.Height);

            if (emotion != null)
            {

                recognizedFace.Anger = emotion.Scores.Anger;
                recognizedFace.Contempt = emotion.Scores.Contempt;
                recognizedFace.Disgust = emotion.Scores.Disgust;
                recognizedFace.Fear = emotion.Scores.Fear;
                recognizedFace.Happiness = emotion.Scores.Happiness;
                recognizedFace.Neutral = emotion.Scores.Neutral;
                recognizedFace.Sadness = emotion.Scores.Sadness;
                recognizedFace.Surprise = emotion.Scores.Surprise;
    }
        }
    }


}
