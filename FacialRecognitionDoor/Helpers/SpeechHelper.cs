using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using Windows.Foundation;



namespace FacialRecognitionDoor.Helpers
{
    /// <summary>
    /// Utilizes SpeechSynthesizer to convert text to an audio message played through a XAML MediaElement
    /// </summary>
    class SpeechHelper : IDisposable
    {

       internal class speechTask
        {
           internal speechTask(string text, bool isSsml)
            {
                this.text = text;
                this.isSsml = isSsml;               
            }
            internal string text { get;  private set; }
            internal bool isSsml { get; private set;}           
        }


        private MediaElement mediaElement;
        private SpeechSynthesizer synthesizer;
        private Semaphore semaphore = new Semaphore(1, 1);

//        private static Mutex mutex = new Mutex(false, "speechMutex");        
        private Queue<speechTask> textToSpeak = new Queue<speechTask>();

        /// <summary>
        /// Accepts a MediaElement that should be placed on whichever page user is on when text is read by SpeechHelper.
        /// Initializes SpeechSynthesizer.
        /// </summary>
        public SpeechHelper(MediaElement media)
        {
            mediaElement = media;
            synthesizer = new SpeechSynthesizer();
            mediaElement.MediaEnded += MediaElement_MediaEnded;
            mediaElement.MediaFailed += MediaElement_MediaFailed;
           // Get all of the installed voices.
           var voices = SpeechSynthesizer.AllVoices;

            // Get the currently selected voice.
            VoiceInformation currentVoice = synthesizer.Voice;

            foreach (VoiceInformation voice in voices)
            {
                if (voice.DisplayName.Contains("Pavel"))
                {
                    synthesizer.Voice = voice;
                    break;
                }                    
            }        
        }

        private void MediaElement_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            semaphore.Release();
        }

        private void MediaElement_MediaEnded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            semaphore.Release();
        }


        /// <summary>
        /// Synthesizes passed through text as audio and plays speech through the MediaElement first sent through.
        /// </summary>
        public async Task Read(string text)
        {
            if (this.textToSpeak.Count < 5)
            {
                speechTask task = new speechTask(text, false);
                this.textToSpeak.Enqueue(task);
            } // drop if we have too many text to speak
        }

        public async Task ProcessSpeechTasksAsync(CancellationToken token)
        {            
                while (!token.IsCancellationRequested)//!cancellationTokenSource.Token.IsCancellationRequested
                {
                if (mediaElement != null && synthesizer != null && textToSpeak.Count > 0)
                {
                    
                    
                    try
                    {
                        semaphore.WaitOne();
                        speechTask task = textToSpeak.Dequeue();
                        if (task != null)
                        {
                            var stream = task.isSsml ? await synthesizer.SynthesizeSsmlToStreamAsync(task.text) :
                                                          await synthesizer.SynthesizeTextToStreamAsync(task.text);
                            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                mediaElement.AutoPlay = true;
                                mediaElement.SetSource(stream, stream.ContentType);
                                mediaElement.Play();
                            });
                            
                        }
                    }
                    catch (Exception ex)
                    {
                        semaphore.Release();
                    }

                    } else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                    }
                }
            
            /*  if (mediaElement != null && synthesizer != null)
            {
                var stream = await synthesizer.SynthesizeSsmlToStreamAsync(text);

                mediaElement.AutoPlay = true;
                mediaElement.SetSource(stream, stream.ContentType);
                mediaElement.Play();
            }
             */
        }

        public async Task ReadSsml(string text)
        {
            speechTask task = new speechTask(text, true);
            this.textToSpeak.Enqueue(task);
        }

        /// <summary>
        /// Disposes of IDisposable type SpeechSynthesizer
        /// </summary>
        public void Dispose()
        {           
            synthesizer.Dispose();
        }
    }
}
