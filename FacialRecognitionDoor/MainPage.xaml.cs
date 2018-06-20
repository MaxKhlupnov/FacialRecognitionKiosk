using FacialRecognitionDoor.FacialRecognition;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using FacialRecognitionDoor.Helpers;
using FacialRecognitionDoor.Objects;
using FacialRecognitionDoor.Controls;
using FacialRecognitionDoor.Views;
using WinRTXamlToolkit.Debugging;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Emotion.Contract;
using ServiceHelpers;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FacialRecognitionDoor
{
    public sealed partial class MainPage : Page
    {


        public event EventHandler FaceDetected;

        // Webcam Related Variables:
        private WebcamHelper webcam;
        static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        // Oxford Related Variables:
        private bool initializedOxford = false;

        // Whitelist Related Variables:
        private List<Member> whitelistedVisitors = new List<Member>();

        Mutex mutexDemographicsData = new Mutex(false, "demographics");
        private DemographicsData demographics = new DemographicsData();

        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        // Speech Related Variables:
        private SpeechHelper speech;


      //  private PirSensorHelper pirSensorHelper;
        private bool motionJustDetected = false;

        // GUI Related Variables:
        private double visitorIDPhotoGridMaxWidth = 0;

        //Debug Console show flag
        private bool isDebugConsoleDisplayed = false;

        private RecognitionPersistence recognitionPersistence;
        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            speech = new SpeechHelper(speechMediaElement);
            if (initializedOxford == false)
            {
                // If Oxford facial recognition has not been initialized, attempt to initialize it
                InitializeOxford();
            }
            this.recognitionPersistence = new RecognitionPersistence();

            Application.Current.Suspending += Current_Suspending;
            Window.Current.Activated += CurrentWindowActivationStateChanged;
            DC.Hide();
            
            LoadDemographics();

            FaceServiceHelper.ApiKeyRegion = "northeurope";

            // Causes this page to save its state when navigating to other pages
            NavigationCacheMode = NavigationCacheMode.Enabled;
            
         
        }

        private async void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            DemographicsData.Save(this.demographics);
        }

        #region CameraControl
        private void CameraControl_CameraAspectRatioChanged(object sender, EventArgs e)
        {
            this.UpdateCameraHostSize();
        }

        private async void CameraControl_AutoCaptureStateChanged(object sender, AutoCaptureState e)
        {
           
            switch (e)
            {
                case AutoCaptureState.WaitingForFaces:
                    this.cameraGuideBallon.Opacity = 1;
                    this.cameraGuideText.Text = "Пожалуйста, встаньте перед камерой!";
                    await speech.Read(this.cameraGuideText.Text);
                    this.cameraGuideHost.Opacity = 1;
                    break;
                case AutoCaptureState.WaitingForStillFaces:
                    this.cameraGuideText.Text = "Пожалуйста, не двигайтесь...";
                    break;
                case AutoCaptureState.ShowingCountdownForCapture:
                    this.cameraGuideText.Text = "";
                    this.cameraGuideBallon.Opacity = 0;

                    this.cameraGuideCountdownHost.Opacity = 1;
                    this.countDownTextBlock.Text = "3";
                    await Task.Delay(350);
                    this.countDownTextBlock.Text = "2";
                    await Task.Delay(350);
                    this.countDownTextBlock.Text = "1";
                    await Task.Delay(350);
                    this.cameraGuideCountdownHost.Opacity = 0;

                    this.ProcessCameraCapture(await this.cameraControl.TakeAutoCapturePhoto());

                    break;
                case AutoCaptureState.ShowingCapturedPhoto:
                    this.cameraGuideHost.Opacity = 0;
                    break;
                default:
                    break;
            }            
        }

        private void LoadDemographics()
        {
            // Todo read demographics data from settings
            this.demographics.StartTime = DateTime.Now;
            this.demographics.Visitors = new List<Visitor>();
            this.demographics.AgeGenderDistribution = new AgeGenderDistribution();
            this.demographics.AgeGenderDistribution.MaleDistribution = new AgeDistribution();
            this.demographics.AgeGenderDistribution.FemaleDistribution = new AgeDistribution();

            // Restore saved settings
            Task.Run( async () => {
                DemographicsData savedData = await DemographicsData.Load();
                if (savedData != null)
                {
                    this.demographics = savedData;
                    this.ageGenderDistributionControl.UpdateData(this.demographics);                
                    this.overallStatsControl.UpdateData(this.demographics);
            }
            });

            
            
        }

        private void UpdateCameraHostSize()
        {
            this.cameraHostGrid.Width = this.cameraHostGrid.ActualHeight * (this.cameraControl.CameraAspectRatio != 0 ? this.cameraControl.CameraAspectRatio : 1.777777777777);
        }

        private async void ProcessCameraCapture(ImageAnalyzer e)
        {
            if (e == null)
            {
                this.cameraControl.RestartAutoCaptureCycle();
                return;
            }

            this.recognitionPersistence.PersisteRecognitionResults(e);

            this.imageFromCameraWithFaces.DataContext = e;
                this.imageFromCameraWithFaces.Visibility = Visibility.Visible;

                

                e.FaceRecognitionCompleted += async (s, args) =>
                {
                    try
                    {
                        ImageAnalyzer results = s as ImageAnalyzer;


                        if (results != null && results.DetectedFaces != null)
                        {

                            foreach (Face detectedFace in results.DetectedFaces)
                            {
                                IdentifiedPerson faceIdIdentification = null;
                                if (results.IdentifiedPersons != null && results.IdentifiedPersons.Count<IdentifiedPerson>() > 0)
                                {
                                    faceIdIdentification = results.IdentifiedPersons.FirstOrDefault(p => p.FaceId == detectedFace.FaceId);
                                }

                                string message = string.Empty;
                                if (faceIdIdentification != null)
                                {
                                    // We able identify this person. Say his name
                                    message = SpeechContants.GeneralGreetigMessage(faceIdIdentification.Person.Name);
                                   await speech.Read(message);
                                }
                                else
                                {
                                    HSFace face = new HSFace(detectedFace.FaceId, "", detectedFace.FaceAttributes);
                                    if (face.Age > 0)
                                    {
                                        // Unknown person!
                                        message = String.Format(SpeechContants.UnknownVisitorDetailMessage, face.Gender, face.Age);
                                        await speech.ReadSsml(message);
                                    }
                                }
                                                                    
                            }
                        }

                        //Update visitors statistic
                        Task.Run( async () => { CalculateVisitorsStatistic(results); });

                        this.photoCaptureBalloonHost.Opacity = 1;

                        int photoDisplayDuration = 10;
                        double decrementPerSecond = 100.0 / photoDisplayDuration;
                        for (double i = 100; i >= 0; i -= decrementPerSecond)
                        {
                            this.resultDisplayTimerUI.Value = i;
                            await Task.Delay(1000);
                        }

                        this.photoCaptureBalloonHost.Opacity = 0;
                        this.imageFromCameraWithFaces.DataContext = null;
                    }
                    finally
                    {
                        this.cameraControl.RestartAutoCaptureCycle();
                    }
                };
               
        }

        #endregion

        private async void CurrentWindowActivationStateChanged(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if ((e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.CodeActivated ||
                e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.PointerActivated) &&
                this.cameraControl.CameraStreamState != Windows.Media.Devices.CameraStreamState.Shutdown)
            {
                // When our Window loses focus due to user interaction Windows shuts it down, so we 
                // detect here when the window regains focus and trigger a restart of the camera.
                await this.cameraControl.StartStreamAsync();
            }
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if(initializedOxford)
            {
                UpdateWhitelistedVisitors();
            }
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes Oxford facial recognition.
        /// </summary>
        public async void InitializeOxford()
        {
            // initializedOxford bool will be set to true when Oxford has finished initialization successfully
            initializedOxford = await OxfordFaceAPIHelper.InitializeOxford(this.speech);

            // Populates UI grid with whitelisted visitors
            UpdateWhitelistedVisitors();

            if (initializedOxford)
            {
                this.imageFromCameraWithFaces.PerformRecognition = true;                
                await speech.Read("Система распознавания лиц инициализирована успешно.");

                this.cameraControl.EnableAutoCaptureMode = true;
                this.cameraControl.FilterOutSmallFaces = true;
                this.cameraControl.AutoCaptureStateChanged += CameraControl_AutoCaptureStateChanged;
                this.cameraControl.CameraAspectRatioChanged += CameraControl_CameraAspectRatioChanged;

                //   this.cameraControl.CameraRestarted += CameraControl_CameraRestarted;

                // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed:
                if (GeneralConstants.DisableLiveCameraFeed)
                {
                    cameraHostGrid.Visibility = Visibility.Collapsed;
                    DisabledFeedGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    cameraHostGrid.Visibility = Visibility.Visible;
                    DisabledFeedGrid.Visibility = Visibility.Collapsed;
                }

            }
            else
                await speech.Read("Ошибка инициализации системы распознавания лиц.");
        }


        /// <summary>
        /// Triggered if we recognize face
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="face"></param>
        private async void CalculateVisitorsStatistic(ImageAnalyzer image)
        {
            if (image == null || !initializedOxford)
                return;

            if (image.DetectedFaces != null)
            {
                foreach(Face face in image.DetectedFaces)
                {
                    //ToDO
                    IdentifiedPerson faceIdIdentification = null;
                    if (image.IdentifiedPersons != null && image.IdentifiedPersons.Count<IdentifiedPerson>() > 0)
                    {
                        faceIdIdentification = image.IdentifiedPersons.FirstOrDefault(p => p.FaceId == face.FaceId);
                    }

                    Visitor detectedPerson = new Visitor { UniqueId = face.FaceId, Count = 1 };
                    bool demographicsChanged = true;
                    if (faceIdIdentification == null)
                    {
                        // New visitor
                        UpdateAgeDistribution(face);
                        this.demographics.Visitors.Add(detectedPerson);
                    }
                    else
                    {
                        var exisitingVisitor = this.demographics.Visitors.FirstOrDefault<Visitor>(v => v.UniqueId == face.FaceId) as Visitor;
                        // exisiting visitor
                        if (exisitingVisitor != null)
                            exisitingVisitor.Count++;

                        demographicsChanged = false;
                    }

                    if (demographicsChanged)
                    {
                        this.ageGenderDistributionControl.UpdateData(this.demographics);
                    }

                    this.overallStatsControl.UpdateData(this.demographics);

                    UpdateDemographicsData(face);

                }

            }

                      
           
        }


        private void UpdateDemographicsData(Face face)
        {
            try
            {
                Visitor detectedPerson = new Visitor { UniqueId = face.FaceId, Count = 1 };

                bool demographicsChanged = true;
                var exisitingVisitor = this.demographics.Visitors.FirstOrDefault<Visitor>(v => v.UniqueId == face.FaceId) as Visitor;
                if (exisitingVisitor == null)
                {
                    // New visitor
                    UpdateAgeDistribution(face);
                    this.demographics.Visitors.Add(detectedPerson);
                }
                else
                {
                    // exisiting visitor
                    exisitingVisitor.Count++;
                    demographicsChanged = false;
                }

                if (demographicsChanged)
                {
                    this.ageGenderDistributionControl.UpdateData(this.demographics);
                }

                this.overallStatsControl.UpdateData(this.demographics);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Demographics Data Update Failed for face." + face.FaceId);
            }
            
        }

        private void UpdateAgeDistribution(Face face)
        {

            AgeDistribution genderBasedAgeDistribution = null;
            if (string.Compare(face.FaceAttributes.Gender, "male", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.demographics.OverallMaleCount++;
                genderBasedAgeDistribution = this.demographics.AgeGenderDistribution.MaleDistribution;
            }
            else
            {
                this.demographics.OverallFemaleCount++;
                genderBasedAgeDistribution = this.demographics.AgeGenderDistribution.FemaleDistribution;
            }

            if (face.FaceAttributes.Age < 16)
            {
                genderBasedAgeDistribution.Age0To15++;
            }
            else if (face.FaceAttributes.Age < 20)
            {
                genderBasedAgeDistribution.Age16To19++;
            }
            else if (face.FaceAttributes.Age < 30)
            {
                genderBasedAgeDistribution.Age20s++;
            }
            else if (face.FaceAttributes.Age < 40)
            {
                genderBasedAgeDistribution.Age30s++;
            }
            else if (face.FaceAttributes.Age < 50)
            {
                genderBasedAgeDistribution.Age40s++;
            }
            else
            {
                genderBasedAgeDistribution.Age50sAndOlder++;
            }
        }

        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech != null)
            {               
                await speech.Read(SpeechContants.InitialGreetingMessage);            
                // Prevents media element from re-greeting visitor
               // speechMediaElement.AutoPlay = false;
            }
            Task.Run(() => this.speech.ProcessSpeechTasksAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);
        }

        /// <summary>
        /// Triggered when the whitelisted users grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            visitorIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;                 
        }

  

        /// <summary>
        /// Unlocks door and greets visitor
        /// </summary>
        private async void UnlockDoor(HSPerson visitor)
        {
            // Greet visitor
            await speech.Read(SpeechContants.GeneralGreetigMessage(visitor.Name));
           
        }

        /// <summary>
        /// Called when user hits vitual add user button. Navigates to NewUserPage page.
        /// </summary>
        private async void NewUserButton_Click(object sender, RoutedEventArgs e)
        { 
            // Stops camera preview on this page, so that it can be started on NewUserPage
         //   await webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(NewUserPage), this.cameraControl);
        }

        /// <summary>
        /// Updates internal list of of whitelisted visitors (whitelistedVisitors) and the visible UI grid
        /// </summary>
        private async void UpdateWhitelistedVisitors()
        {
            // If the whitelist isn't already being updated, update the whitelist
            if (!currentlyUpdatingWhitelist)
            {
                currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedVisitorsList();
                UpdateWhitelistedVisitorsGrid();
                currentlyUpdatingWhitelist = false;
            }
        }

        /// <summary>
        /// Updates the list of Member objects with all whitelisted visitors stored on disk
        /// </summary>
        private async Task UpdateWhitelistedVisitorsList()
        {
            // Clears whitelist
            whitelistedVisitors.Clear();

            // If the whitelistFolder has not been opened, open it
            if (whitelistFolder == null)
            {
                whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates subFolders list with all sub folders within the whitelist folders.
            // Each of these sub folders represents the Id photos for a single visitor.
            var subFolders = await whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in subFolders)
            {
                string visitorName = folder.Name;
                var filesInFolder = await folder.GetFilesAsync();

                var photoStream = await filesInFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage visitorImage = new BitmapImage();
                await visitorImage.SetSourceAsync(photoStream);

                Member whitelistedVisitor = new Member (visitorName, folder, visitorImage, visitorIDPhotoGridMaxWidth);

                whitelistedVisitors.Add(whitelistedVisitor);
            }
        }

        /// <summary>
        /// Updates UserInterface list of whitelisted users from the list of Member objects (WhitelistedVisitors)
        /// </summary>
        private void UpdateWhitelistedVisitorsGrid()
        {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Visitor>();
            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            WhitelistedUsersGrid.ItemsSource = whitelistedVisitors;

            // Hide Oxford loading ring
            OxfordLoadingRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Triggered when the user selects a visitor in the WhitelistedUsersGrid 
        /// </summary>
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UserProfilePage, passing through the selected Member object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Member, webcam));
        }

        /// <summary>
        /// Triggered when the user selects the Shutdown button in the app bar. Closes app.
        /// </summary>
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }

        private void DebugConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDebugConsoleDisplayed)
                DC.Hide();
            else
                DC.ShowLog();

            isDebugConsoleDisplayed = !isDebugConsoleDisplayed;
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {          

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(SettingsPage), webcam);
        }

        private void overallStatsControl_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
