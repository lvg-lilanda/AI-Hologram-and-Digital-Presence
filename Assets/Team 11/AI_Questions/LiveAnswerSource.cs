using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Team11.AI
{
    /// Seam A, live. Calls the local AI service instead of reading a clip off disk.
    /// The contract is docs/AI_SERVICE_CONTRACT.md - if a field name here stops
    /// matching the service, the symptom is a silently empty string, not an error,
    /// because JsonUtility maps JSON keys to field names literally and ignores the
    /// rest. Do not "tidy" these to PascalCase.
    ///
    /// Everything happens on the main thread: UnityWebRequest can only be created
    /// and sent there, and AvatarController's async handler resumes on Unity's
    /// synchronisation context, so awaiting here is safe.
    public sealed class LiveAnswerSource : IAnswerSource
    {
        private readonly string _baseUrl;
        private readonly int _timeoutSeconds;
        private readonly string _sessionId;

        public LiveAnswerSource(string baseUrl, string sessionId, int timeoutSeconds = 30)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _sessionId = sessionId;
            _timeoutSeconds = timeoutSeconds;
        }

        /// The last failure, for the transcript log. Null when the last call worked.
        public string LastError { get; private set; }

        public async Task<Answer> AskAsync(string question, CancellationToken ct)
        {
            LastError = null;

            var payload = JsonUtility.ToJson(new AnswerRequestDto
            {
                text = question,
                sessionId = _sessionId,
                speak = true,
            });

            AnswerResponseDto dto;
            using (var request = new UnityWebRequest($"{_baseUrl}/answer", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(
                    System.Text.Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = _timeoutSeconds;

                await SendAsync(request, ct);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LastError = DescribeFailure(request);
                    Debug.LogWarning($"[LiveAnswerSource] {LastError}");
                    return null;
                }

                dto = JsonUtility.FromJson<AnswerResponseDto>(request.downloadHandler.text);
            }

            if (dto == null)
            {
                LastError = "response was not the agreed JSON shape";
                return null;
            }

            // No audio is a normal outcome, not a failure: the service returns a null
            // audio URL whenever speech is unconfigured. AvatarController captions the
            // text in that case rather than dropping the answer.
            AudioClip clip = string.IsNullOrEmpty(dto.audioUrl)
                           ? null
                           : await FetchClipAsync(dto.audioUrl, ct);

            string visemeJson = string.IsNullOrEmpty(dto.visemesUrl)
                              ? null
                              : await FetchTextAsync(dto.visemesUrl, ct);

            if (!string.IsNullOrEmpty(dto.speechUnavailable))
            {
                Debug.Log($"[LiveAnswerSource] no audio: {dto.speechUnavailable}");
            }

            return new Answer
            {
                Id         = dto.id,
                Text       = dto.text,
                Audio      = clip,
                VisemeJson = visemeJson,
                FollowUps  = dto.followUps,
                IsFallback = false,
            };
        }

        // --- transport ------------------------------------------------------

        private async Task<AudioClip> FetchClipAsync(string path, CancellationToken ct)
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip(
                       $"{_baseUrl}{path}", AudioType.WAV))
            {
                request.timeout = _timeoutSeconds;
                await SendAsync(request, ct);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[LiveAnswerSource] audio fetch failed: {request.error}");
                    return null;
                }
                return DownloadHandlerAudioClip.GetContent(request);
            }
        }

        private async Task<string> FetchTextAsync(string path, CancellationToken ct)
        {
            using (var request = UnityWebRequest.Get($"{_baseUrl}{path}"))
            {
                request.timeout = _timeoutSeconds;
                await SendAsync(request, ct);
                return request.result == UnityWebRequest.Result.Success
                     ? request.downloadHandler.text
                     : null;
            }
        }

        /// Bridges UnityWebRequest's operation to a Task, and honours cancellation -
        /// without the Abort, cancelling a question leaves the request running and the
        /// answer arrives after the avatar has moved on.
        private static Task SendAsync(UnityWebRequest request, CancellationToken ct)
        {
            var done = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();

            CancellationTokenRegistration registration = default;
            if (ct.CanBeCanceled)
            {
                registration = ct.Register(() =>
                {
                    if (!request.isDone) request.Abort();
                });
            }

            operation.completed += _ =>
            {
                registration.Dispose();
                done.TrySetResult(true);
            };
            return done.Task;
        }

        private static string DescribeFailure(UnityWebRequest request)
        {
            var body = request.downloadHandler?.text;
            if (!string.IsNullOrEmpty(body) && body.Contains("\"code\""))
            {
                var error = JsonUtility.FromJson<ErrorEnvelopeDto>(body);
                if (error?.error != null && !string.IsNullOrEmpty(error.error.code))
                {
                    return $"{error.error.code}: {error.error.message} " +
                           $"({(error.error.retryable ? "retryable" : "do not retry")})";
                }
            }
            return $"{request.responseCode} {request.error}";
        }

        // --- wire format ----------------------------------------------------
        // Only the fields the avatar needs. JsonUtility ignores the rest of the
        // response, which is deliberate: `citations[].page` is null for markdown
        // sources and JsonUtility cannot represent a nullable int, so parsing
        // citations here would break on exactly the sources the corpus uses most.

        [Serializable]
        private class AnswerRequestDto
        {
            public string text;
            public string sessionId;
            public bool speak;
        }

        [Serializable]
        private class AnswerResponseDto
        {
            public string id;
            public string text;
            public string[] followUps;
            public string outcome;
            public bool grounded;
            public bool isFallback;
            public string audioUrl;
            public string visemesUrl;
            public string speechUnavailable;
        }

        [Serializable]
        private class ErrorDto
        {
            public string code;
            public string message;
            public bool retryable;
        }

        [Serializable]
        private class ErrorEnvelopeDto
        {
            public ErrorDto error;
        }
    }
}
