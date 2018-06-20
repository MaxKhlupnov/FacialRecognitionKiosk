using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using FacialRecognitionDoor.Helpers;
using WinRTXamlToolkit.Debugging;
using Windows.Storage;
using ClientContract = Microsoft.ProjectOxford.Face.Contract;
using ServiceHelpers;

namespace FacialRecognitionDoor.FacialRecognition
{
    class FaceApiRecognizer : IFaceRecognizer
    {
        #region Private members
        private static readonly Lazy<FaceApiRecognizer> _recognizer = new Lazy<FaceApiRecognizer>( () => new FaceApiRecognizer());

        private FaceApiWhitelist _whitelist = null;
       // private IFaceServiceClient _faceApiClient = null;    
        private StorageFolder _whitelistFolder = null;
        #endregion

        #region Properties
        /// <summary>
        /// Face API Recognizer instance
        /// </summary>
        public static FaceApiRecognizer Instance
        {
            get
            {
                return _recognizer.Value;
            }
        }

        internal SpeechHelper speechHelper { get; set; }

        /// <summary>
        /// Whitelist Id on Cloud Face API
        /// </summary>
        public string WhitelistId
        {
            get;
            private set;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// Initial Face Api client
        /// </summary>
        private FaceApiRecognizer() {
           // _faceApiClient = new FaceServiceClient(GeneralConstants.OxfordAPIKey);
        }
        #endregion

        #region Whitelist

        private void UpdateProgress(IProgress<int> progress, double progressCnt)
        {
            if(progress != null)
            {
                progress.Report((int)Math.Round(progressCnt));
            }
        }

        /// <summary>
        /// Train whitelist until training finished
        /// </summary>
        /// <returns></returns>
        private async Task<bool> TrainingWhitelistAsync()
        {
            bool isSuccess = true;

            // Train whitelist after add all person
            Debug.WriteLine("Start training whitelist...");
            await FaceServiceHelper.TrainPersonGroupAsync(WhitelistId);

            ClientContract.TrainingStatus status;

            while(true)
            {
                status = await FaceServiceHelper.GetPersonGroupTrainingStatusAsync(WhitelistId);

                // if still running, continue to check status
                if(status.Status == ClientContract.Status.Running)
                {
                    continue;
                }

                // if timeout or failed
                if (status.Status != ClientContract.Status.Succeeded)
                {
                    isSuccess = false;
                }
                break;
            }

            return isSuccess;
        }

        public async Task<bool> CreateWhitelistFromFolderAsync(string whitelistId, StorageFolder whitelistFolder = null, IProgress<int> progress = null)
        {
            bool isSuccess = true;
            double progressCnt = 0;

            WhitelistId = whitelistId;
            _whitelist = new FaceApiWhitelist(WhitelistId);

            try
            {
                // whitelist folder default to picture library
                if (whitelistFolder == null)
                {
                    whitelistFolder = await KnownFolders.PicturesLibrary.GetFolderAsync("WhiteList");
                }

                _whitelistFolder = whitelistFolder;

                // detele person group if already exists
                try
                {
                    // An exception throwed if the person group doesn't exist
                    await FaceServiceHelper.GetPersonGroupAsync(whitelistId);
                    UpdateProgress(progress, ++progressCnt);

                    await FaceServiceHelper.DeletePersonGroupAsync(whitelistId);
                    UpdateProgress(progress, ++progressCnt);

                    Debug.WriteLine("Deleted old group");
                }
                catch(FaceAPIException ce)
                { 
                    // Group not found
                    if(ce.ErrorCode == "PersonGroupNotFound")
                    {
                        Debug.WriteLine("The group doesn't exists before");
                        speechError("Создание новой группы доступа ");
                    }
                    else
                    {
                        throw ce;
                    }
                }

                await FaceServiceHelper.CreatePersonGroupAsync(WhitelistId, "White List", string.Empty);
                UpdateProgress(progress, ++progressCnt);

                await BuildWhiteListAsync(progress, progressCnt);
            }
            catch(FaceAPIException ce)
            {
                isSuccess = false;
                Debug.WriteLine("ClientException in CreateWhitelistFromFolderAsync : " + ce.ErrorCode);
                speechError("Ошибка подключения к ФэйсАпи: " + ce.ErrorMessage);
            }
            catch(Exception e)
            {
                isSuccess = false;
                Debug.WriteLine("Exception in CreateWhitelistFromFolderAsync : " + e.Message);
                speechError("Ошибка регистрации списка доступа на сервисе: " + e.Message);
            }

            // progress to 100%
            UpdateProgress(progress, 100);

            return isSuccess;
        }

        private async void speechError(string errorMessage)
        {
            if (this.speechHelper != null)
                this.speechHelper.Read(errorMessage);
        }

        /// <summary>
        /// Use whitelist folder to build whitelist Database
        /// </summary>
        /// <returns></returns>
        private async Task BuildWhiteListAsync(IProgress<int> progress, double progressCnt)
        {
            Debug.WriteLine("Start building whitelist from " + _whitelistFolder.Path);

            // calc progress step
            var fileCnt = await FaceApiUtils.GetFileCountInWhitelist(_whitelistFolder);
            var progressStep = (100.0 - progressCnt) / fileCnt;

            var subFolders = await _whitelistFolder.GetFoldersAsync();
            // Iterate all subfolders in whitelist
            List<Task> tasks = new List<Task>();

            foreach (var folder in subFolders)
            {
                var personName = folder.Name;

                // create new person
                var personId = await CreatePerson(personName, folder);

                // get all images in the folder
                var files = await folder.GetFilesAsync();                
                // iterate all images and add to whitelist
                foreach (var img in await folder.GetFilesAsync())
                {
                    
                    tasks.Add(StartAddFaceImageTask(img, personId).Unwrap().ContinueWith((taskDone) =>
                      {
                          var detectTask = taskDone as Task<Tuple<string, ClientContract.AddPersistedFaceResult>>;

                          if (detectTask == null)
                              return;

                          // Update something on UI
                          var faceDetectionResult = detectTask.Result;
                          if (faceDetectionResult != null && faceDetectionResult.Item2 != null && faceDetectionResult.Item1 != null)
                              _whitelist.AddFace(personId, faceDetectionResult.Item2.PersistedFaceId, faceDetectionResult.Item1);

                          else
                              return;


                          // update progress
                          progressCnt += progressStep;
                          UpdateProgress(progress, progressCnt);
                      }
                      ));                   
                }
                await Task.WhenAll(tasks);
            }

            await Task.WhenAll(tasks);
            await TrainingWhitelistAsync();

            Debug.WriteLine("Whitelist created successfully!");

        }
        #endregion

        #region Face
        /// <summary>
        /// Add face to both Cloud Face API and local whitelist
        /// </summary>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private async Task AddFace(Guid personId,  string imagePath)
        {
            using (Stream s = File.OpenRead(imagePath))
            {
                try
                {
                    // await _faceApiClient.AddPersonFaceAsync(WhitelistId, personId, faceId.ToString(), imagePath);
                    var faceDetectionResult = await FaceServiceHelper.AddPersonFaceAsync(WhitelistId, personId, s);
                    _whitelist.AddFace(personId, faceDetectionResult.PersistedFaceId, imagePath);
                }
                catch (FaceAPIException)
                {
                    // Here we simply ignore all detection failure in this sample
                    // You may handle these exceptions by check the Error.Error.Code and Error.Message property for ClientException object
                    // return new Tuple<string, Microsoft.ClientContract.Face[]>(imgPath, null);
                    Debug.WriteLine(String.Format("Can't detect any face in image {0}", imagePath));
                }
            }
        }

        /// <summary>
        /// Remove face from both Cloud Face API and local whitelist
        /// </summary>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <returns></returns>
        private async Task RemoveFace(Guid personId, Guid faceId)
        {
            await FaceServiceHelper.DeletePersonFaceAsync(WhitelistId, personId, faceId);
            _whitelist.RemoveFace(personId, faceId);
        }

        /// <summary>
        /// Detect face and return the face id of a image file
        /// </summary>
        /// <param name="imageFile">
        /// image file to detect face
        /// Note: the image must only contains exactly one face
        /// </param>
        /// <returns>face id</returns>
        private async Task<Guid> DetectFaceFromImage(StorageFile imageFile)
        {
            var stream = await imageFile.OpenStreamForReadAsync();
            var faces = await FaceServiceHelper.DetectAsync(stream);
            if(faces == null || faces.Length < 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.NoFaceDetected);
            }
            else if(faces.Length > 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.MultipleFacesDetected);
            }

            return faces[0].FaceId;
        }

        /// <summary>
        /// Detect face and return the face id of a image file
        /// </summary>
        /// <param name="imageFile">
        /// image file to detect face
        /// </param>
        /// <returns>face id</returns>
        private async Task<ClientContract.Face[]> DetectFacesFromImage(StorageFile imageFile)
        {
            var stream = await imageFile.OpenStreamForReadAsync();
            var faces = await FaceServiceHelper.DetectAsync(stream, 
                true, false, new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile });
            if (faces == null || faces.Length < 1)
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.NoFaceDetected);
            }

            return faces;//FaceApiUtils.FacesToFaceIds(faces) ;
        }

        public async Task<bool> AddImageToWhitelistAsync(StorageFile imageFile, string personName = null)
        {
            bool isSuccess = true;

            // imageFile should be valid image file
            if (!FaceApiUtils.ValidateImageFile(imageFile))
            {
                isSuccess = false;
            }
            else
            {
                var filePath = imageFile.Path;

                // If personName is null/empty, use the folder name as person name
                if(string.IsNullOrEmpty(personName))
                {
                    personName = await FaceApiUtils.GetParentFolderNameAsync(imageFile);
                }

                // If person name doesn't exists, add it
                var personId = _whitelist.GetPersonIdByName(personName);
                if(personId == Guid.Empty)
                {
                    var folder = await imageFile.GetParentAsync();
                    personId = await CreatePerson(personName, folder);
                }

                // detect faces
                //   var faceId = await DetectFaceFromImage(imageFile);
                //   await AddFace(personId, imageFile.Path);
                Task task = StartAddFaceImageTask(imageFile, personId);
                task.ContinueWith((taskDone) =>
                {
                    var detectTask = taskDone as Task<Tuple<string, ClientContract.AddPersistedFaceResult>>;
                    // Update something on UI
                    var faceDetectionResult = detectTask.Result;
                    if (faceDetectionResult != null && faceDetectionResult.Item2 != null)
                        _whitelist.AddFace(personId, faceDetectionResult.Item2.PersistedFaceId, faceDetectionResult.Item1);
                    else
                        Debug.WriteLine("Error detecting face in image " + imageFile.Path);

                }).Wait();

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;
        }


        private Task<Task<Tuple<string, ClientContract.AddPersistedFaceResult>>> StartAddFaceImageTask(StorageFile imageFile, Guid personId)
        {
           return Task.Factory.StartNew(
               async (obj) =>
               {
                   
                   Debug.WriteLine("BuildWhiteList: Processing " + imageFile.Path);

                   try
                   {
                       using (var fStream = await imageFile.OpenStreamForReadAsync())
                       {
                           var faceDetectionResult = await FaceServiceHelper.AddPersonFaceAsync(WhitelistId, personId, fStream);
                           return new Tuple<string, ClientContract.AddPersistedFaceResult>(imageFile.Path, faceDetectionResult);
                       }
                   }
                   catch (FaceAPIException)
                   {
                       Debug.WriteLine("Error detecting face in image " + imageFile.Path);
                       // Here we simply ignore all detection failure in this sample
                       // You may handle these exceptions by check the Error.Error.Code and Error.Message property for ClientException object
                       return new Tuple<string, ClientContract.AddPersistedFaceResult>(imageFile.Path, null);
                   }

               }, imageFile);
        }


        public async Task<bool> RemoveImageFromWhitelistAsync(StorageFile imageFile, string personName = null)
        {
            bool isSuccess = true;
            if (!FaceApiUtils.ValidateImageFile(imageFile))
            {
                isSuccess = false;
            }
            else
            {
                // If personName is null use the folder name as person name
                if(string.IsNullOrEmpty(personName))
                {
                    personName = await FaceApiUtils.GetParentFolderNameAsync(imageFile);
                }

                var personId = _whitelist.GetPersonIdByName(personName);
                var faceId = _whitelist.GetFaceIdByFilePath(imageFile.Path);
                if(personId == Guid.Empty || faceId == Guid.Empty)
                {
                    isSuccess = false;
                }
                else
                {
                    await RemoveFace(personId, faceId);

                    // train whitelist
                    isSuccess = await TrainingWhitelistAsync();
                }
            }
            return isSuccess;
        }
        #endregion

        #region Person
        /// <summary>
        /// Create a person into Face API and whitelist
        /// </summary>
        /// <param name="personName"></param>
        /// <param name="personFolder"></param>
        /// <returns></returns>
        private async Task<Guid> CreatePerson(string personName, StorageFolder personFolder)
        {
            var ret = await FaceServiceHelper.CreatePersonAsync(WhitelistId, personName);
            var personId = ret.PersonId;

            _whitelist.AddPerson(personId, personName, personFolder.Path);

            return personId;
        }

        private async Task RemovePerson(Guid personId)
        {
            await FaceServiceHelper.DeletePersonAsync(WhitelistId, personId);
            _whitelist.RemovePerson(personId);
        }

        public async Task<bool> AddPersonToWhitelistAsync(StorageFolder faceImagesFolder, string personName = null)
        {
            bool isSuccess = true;

            if(faceImagesFolder == null)
            {
                isSuccess = false;
            }
            else
            {
                // use folder name if do not have personName
                if(string.IsNullOrEmpty(personName))
                {
                    personName = faceImagesFolder.Name;
                }

                var personId = await CreatePerson(personName, faceImagesFolder);
                var files = await faceImagesFolder.GetFilesAsync();

                // iterate all files and add to whitelist
                foreach(var file in files)
                {
                    try
                    {
                        // detect faces
                        var faceId = await DetectFaceFromImage(file);
                        await AddFace(personId,  file.Path);
                    }
                    catch(FaceRecognitionException fe)
                    {
                        switch (fe.ExceptionType)
                        {
                            case FaceRecognitionExceptionType.InvalidImage:
                                Debug.WriteLine("WARNING: This file is not a valid image!");
                                break;
                            case FaceRecognitionExceptionType.NoFaceDetected:
                                Debug.WriteLine("WARNING: No face detected in this image");
                                break;
                            case FaceRecognitionExceptionType.MultipleFacesDetected:
                                Debug.WriteLine("WARNING: Multiple faces detected, ignored this image");
                                break;
                        }
                    }
                }

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;                
        }

        public async Task<bool> RemovePersonFromWhitelistAsync(string personName)
        {
            bool isSuccess = true;

            var personId = _whitelist.GetPersonIdByName(personName);
            if(personId == Guid.Empty)
            {
                isSuccess = false;
            }
            else
            {
                // remove all faces belongs to this person
                var faceIds = _whitelist.GetAllFaceIdsByPersonId(personId);
                if(faceIds != null)
                {
                    var faceIdsArr = faceIds.ToArray();
                    for (int i = 0; i < faceIdsArr.Length; i++)
                    {
                        await RemoveFace(personId, faceIdsArr[i]);
                    }
                }

                // remove person
                await RemovePerson(personId);

                // train whitelist
                isSuccess = await TrainingWhitelistAsync();
            }

            return isSuccess;
        }
        #endregion

        #region Face recognition
        public async Task<Dictionary<HSFace, HSPerson>> FaceRecognizeAsync(StorageFile imageFile)
        {
            var recogResult = new Dictionary<HSFace, HSPerson>();

            if(!FaceApiUtils.ValidateImageFile(imageFile))
            {
                throw new FaceRecognitionException(FaceRecognitionExceptionType.InvalidImage);
            }

            // detect all faces in the image
            var arFaces = await DetectFacesFromImage(imageFile);

            // try to identify all faces to person
            var identificationResults = await FaceServiceHelper.IdentifyAsync(WhitelistId, FaceApiUtils.FacesToFaceIds(arFaces));

            // add identified person name to result list
            foreach(var result in identificationResults)
            {                                
                if (result.Candidates.Length > 0)
                {                    
                    var person = _whitelist.GetPersonById(result.Candidates[0].PersonId);
                    Debug.WriteLine(String.Format("Detected person {0} face ID Confidence: {1}% image file: {2}", 
                        person.Name, Math.Round(result.Candidates[0].Confidence * 100, 1), imageFile.Path));                    
                    recogResult.Add(new HSFace(result.FaceId, imageFile.Path),person);
                }
                else
                {
                   foreach(ClientContract.Face detectedFace in arFaces)
                    {
                        if (detectedFace.FaceId == result.FaceId)
                        {
                            recogResult.Add(new HSFace(result.FaceId, imageFile.Path, detectedFace.FaceAttributes), null);
                            Debug.WriteLine(String.Format("Unknown {0} age {1} face ID {2} image file: {3}", detectedFace.FaceAttributes.Gender,
                               detectedFace.FaceAttributes.Age, detectedFace.FaceId, imageFile.Path));
                        
                        }
                    }
                }               
            }

            return recogResult;
        }
        #endregion
    }
}
