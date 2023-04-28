using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FernGraph;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Random = UnityEngine.Random;

namespace StableDiffusionGraph.SDGraph.Nodes
{
    [Node(Path = "SD Standard")]
    [Tags("SD Node")]
    public class SDImg2ImgNode : SDFlowNode, ICanExecuteSDFlow
    {
        [Input("In Image")] public Texture2D InputImage;
        [Input("Mask")] public Texture2D MaskImage;
        [Input("ControlNet")] public ControlNetData controlNetData;
        [Input] public Prompt Prompt;
        [Input] public int Step = 20;
        [Input] public int CFG = 7;
        [Input] public float DenisoStrength = 0.75f;
        [Output("Out Image")] public Texture2D OutputImage;
        [Output("Seed")] public long outSeed;

        public long Seed = -1;
        public string SamplerMethod = "Euler";
        public int inpainting_fill = 0;
        public bool inpaint_full_res = true;
        public int inpaint_full_res_padding = 32;
        public int inpainting_mask_invert = 0;
        public int mask_blur = 0;

        public Action<long, long> OnUpdateSeedField;

        private int width = 512;
        private int height = 512;
        private float aspect;

        public override IEnumerator Execute()
        {
            Prompt = GetInputValue("Prompt", this.Prompt);
            controlNetData = GetInputValue("ControlNet", controlNetData);
            InputImage = GetInputValue("In Image", this.InputImage);
            MaskImage = GetInputValue("Mask", this.MaskImage);

            var vec2 = SDUtil.GetMainGameViewSize();
            width = (int)vec2.x;
            height = (int)vec2.y;
            
            if (InputImage != null)
            {
                if (Seed == 0)
                {
                    Seed = GenerateRandomLong(-1, Int64.MaxValue);
                }
                yield return (GenerateAsync());
            }

            yield return null;
        }
        
        long GenerateRandomLong(long min, long max)
        {
            byte[] buf = new byte[8];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return (Math.Abs(longRand % (max - min)) + min);
        }

        public override object OnRequestValue(Port port)
        {
            if (port.Name == "Out Image")
            {
                return OutputImage;
            }else if (port.Name == "Seed")
            {
                return outSeed;
            }

            return null;
        }

        IEnumerator GenerateAsync()
        {

            // Generate the image
            HttpWebRequest httpWebRequest = null;
            try
            {
                // Make a HTTP POST request to the Stable Diffusion server
                httpWebRequest =
                    (HttpWebRequest)WebRequest.Create(SDDataHandle.serverURL + SDDataHandle.ImageToImageAPI);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                // add auth-header to request
                if (SDDataHandle.UseAuth && !SDDataHandle.Username.Equals("") && !SDDataHandle.Password.Equals(""))
                {
                    httpWebRequest.PreAuthenticate = true;
                    byte[] bytesToEncode = Encoding.UTF8.GetBytes(SDDataHandle.Username + ":" + SDDataHandle.Password);
                    string encodedCredentials = Convert.ToBase64String(bytesToEncode);
                    httpWebRequest.Headers.Add("Authorization", "Basic " + encodedCredentials);
                }

                // Send the generation parameters along with the POST request
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {

                    byte[] inputImgBytes = InputImage.EncodeToPNG();
                    string inputImgString = Convert.ToBase64String(inputImgBytes);
                    string maskImgString = "";
                    if (MaskImage == null)
                    {
                        MaskImage = new Texture2D(InputImage.width, InputImage.height);
                        Color[] colors = new Color[InputImage.width * InputImage.height];
                        for (var i = 0; i < InputImage.width * InputImage.height; ++i)
                        {
                            colors[i] = Color.white;
                        }

                        MaskImage.SetPixels(colors);
                    }
                    string json;

                    if (controlNetData == null)
                    {
                        SDParamsInImg2Img sd = new SDParamsInImg2Img();
                        byte[] maskImgBytes = MaskImage.EncodeToPNG();
                        maskImgString = Convert.ToBase64String(maskImgBytes);

                        sd.mask = maskImgString;
                        sd.init_images = new string[] { inputImgString };
                        sd.prompt = Prompt.positive;
                        sd.negative_prompt = Prompt.negative;
                        sd.steps = Step;
                        sd.cfg_scale = CFG;
                        sd.denoising_strength = DenisoStrength;
                        sd.width = Screen.width;
                        sd.height = Screen.height;
                        sd.seed = Seed;
                        sd.tiling = false;
                        sd.sampler_name = SamplerMethod;
                        sd.inpainting_fill = inpainting_fill;
                        sd.inpaint_full_res = inpaint_full_res;
                        sd.inpaint_full_res_padding = inpaint_full_res_padding;
                        sd.inpainting_mask_invert = inpainting_mask_invert;
                        sd.mask_blur = mask_blur;
                        // Serialize the input parameters
                        json = JsonConvert.SerializeObject(sd);
                    }
                    else
                    {
                        SDParamsInImg2ImgControlNet sd = new SDParamsInImg2ImgControlNet();
                        byte[] maskImgBytes = MaskImage.EncodeToPNG();
                        maskImgString = Convert.ToBase64String(maskImgBytes);

                        sd.mask = maskImgString;
                        sd.init_images = new string[] { inputImgString };
                        sd.prompt = Prompt.positive;
                        sd.negative_prompt = Prompt.negative;
                        sd.steps = Step;
                        sd.cfg_scale = CFG;
                        sd.denoising_strength = DenisoStrength;
                        sd.width = Screen.width;
                        sd.height = Screen.height;
                        sd.seed = Seed;
                        sd.tiling = false;
                        sd.sampler_name = SamplerMethod;
                        sd.inpainting_fill = inpainting_fill;
                        sd.inpaint_full_res = inpaint_full_res;
                        sd.inpaint_full_res_padding = inpaint_full_res_padding;
                        sd.inpainting_mask_invert = inpainting_mask_invert;
                        sd.mask_blur = mask_blur;
                        if (controlNetData != null)
                        {
                            SDUtil.SDLog("use ControlNet");
                            sd.alwayson_scripts = new ALWAYSONSCRIPTS();
                            sd.alwayson_scripts.controlnet = new ControlNetDataArgs();
                            sd.alwayson_scripts.controlnet.args = new[] { controlNetData };
                        }
                        // Serialize the input parameters
                        json = JsonConvert.SerializeObject(sd);
                    }
                    
                    // Send to the server
                    streamWriter.Write(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "\n\n" + e.StackTrace);
            }

            // Read the output of generation
            if (httpWebRequest != null)
            {
                // Wait that the generation is complete before procedding
                Task<WebResponse> webResponse = httpWebRequest.GetResponseAsync();

                while (!webResponse.IsCompleted)
                {
                    if (SDDataHandle.UseAuth && !SDDataHandle.Username.Equals("") && !SDDataHandle.Password.Equals(""))
                        //UpdateGenerationProgressWithAuth();
                        // else
                        // UpdateGenerationProgress();

                        yield return new WaitForSeconds(0.5f);
                }

                // Stream the result from the server
                var httpResponse = webResponse.Result;

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    // Decode the response as a JSON string
                    string result = streamReader.ReadToEnd();

                    // Deserialize the JSON string into a data structure
                    SDResponseImg2Img json = JsonConvert.DeserializeObject<SDResponseImg2Img>(result);

                    // If no image, there was probably an error so abort
                    if (json.images == null || json.images.Length == 0)
                    {
                        Debug.LogError(
                            "No image was return by the server. This should not happen. Verify that the server is correctly setup.");

#if UNITY_EDITOR
                        EditorUtility.ClearProgressBar();
#endif
                        yield break;
                    }

                    // Decode the image from Base64 string into an array of bytes
                    byte[] imageData = Convert.FromBase64String(json.images[0]);
                    OutputImage = new Texture2D(width, height, DefaultFormat.HDR, TextureCreationFlags.None);
                    OutputImage.LoadImage(imageData);

                    try
                    {
                        // Read the generation info back (only seed should have changed, as the generation picked a particular seed)
                        if (json.info != "")
                        {
                            SDParamsOutTxt2Img info = JsonConvert.DeserializeObject<SDParamsOutTxt2Img>(json.info);

                            // Read the seed that was used by Stable Diffusion to generate this result
                            outSeed = info.seed;
                            Seed = 0;
                            OnUpdateSeedField?.Invoke(Seed, outSeed);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + "\n\n" + e.StackTrace);
                    }
                }
            }

            yield return null;
        }
    }
}