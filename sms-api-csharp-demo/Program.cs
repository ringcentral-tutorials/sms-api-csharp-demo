using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RingCentral;
using Newtonsoft.Json.Linq;

namespace sms_api_csharp_demo
{
    class Program
    {
        const string RECIPIENT_PHONE_NUMBER = "<ENTER PHONE NUMBER>";

        const string RINGCENTRAL_CLIENTID = "<ENTER CLIENT ID>";
        const string RINGCENTRAL_CLIENTSECRET = "<ENTER CLIENT SECRET>";

        const string RINGCENTRAL_USERNAME = "<YOUR ACCOUNT PHONE NUMBER>";
        const string RINGCENTRAL_PASSWORD = "<YOUR ACCOUNT PASSWORD>";
        const string RINGCENTRAL_EXTENSION = "<YOUR EXTENSION, PROBABLY";
        static bool waitLoop = true;
        static void Main(string[] args)
        {
            //send_sms().Wait();
            //send_mms().Wait();
            //retrieve_modify().Wait();
            //retrieve_delete().Wait();
            //receive_reply().Wait();
        }
        static private async Task send_sms()
        {
            RestClient rc = new RestClient(RINGCENTRAL_CLIENTID, RINGCENTRAL_CLIENTSECRET, false);
            await rc.Authorize(RINGCENTRAL_USERNAME, RINGCENTRAL_EXTENSION, RINGCENTRAL_PASSWORD);
            if (rc.token.access_token.Length > 0)
            {
                var parameters = new CreateSMSMessage();
                parameters.from = new MessageStoreCallerInfoRequest { phoneNumber = RINGCENTRAL_USERNAME };
                parameters.to = new MessageStoreCallerInfoRequest[] { new MessageStoreCallerInfoRequest { phoneNumber = RECIPIENT_PHONE_NUMBER } };
                parameters.text = "This is a test message from C#";

                var resp = await rc.Restapi().Account().Extension().Sms().Post(parameters);
                Console.WriteLine("SMS sent. Delivery status: " + resp.messageStatus);
            }
        }
        static private async Task send_mms()
        {
            RestClient rc = new RestClient(RINGCENTRAL_CLIENTID, RINGCENTRAL_CLIENTSECRET, false);
            await rc.Authorize(RINGCENTRAL_USERNAME, RINGCENTRAL_EXTENSION, RINGCENTRAL_PASSWORD);
            if (rc.token.access_token.Length > 0)
            {
                var parameters = new CreateSMSMessage();
                parameters.from = new MessageStoreCallerInfoRequest { phoneNumber = RINGCENTRAL_USERNAME };
                parameters.to = new MessageStoreCallerInfoRequest[] { new MessageStoreCallerInfoRequest { phoneNumber = RECIPIENT_PHONE_NUMBER } };
                parameters.text = "This is a test message from C#";
                var attachment = new Attachment { fileName = "test.jpg", contentType = "image/jpeg", bytes = File.ReadAllBytes("test.jpg") };
                var attachments = new Attachment[] { attachment };
                var resp = await rc.Restapi().Account().Extension().Sms().Post(parameters, attachments);
                Console.WriteLine("MMS sent. Delivery status: " + resp.messageStatus);
                //track_status(rc, resp.id, resp.messageStatus).Wait();
            }
        }
        static private async Task track_status(RestClient rc, String messageId, String messageStatus)
        {
            while(messageStatus == "Queued")
            {
                Thread.Sleep(1000);
                var resp = await rc.Restapi().Account().Extension().MessageStore(messageId).Get();
                messageStatus = resp.messageStatus;
                Console.WriteLine("MMS message delivery status: " + messageStatus);
            }
        }
        static private async Task retrieve_modify()
        {
            RestClient rc = new RestClient(RINGCENTRAL_CLIENTID, RINGCENTRAL_CLIENTSECRET, false);
            await rc.Authorize(RINGCENTRAL_USERNAME, RINGCENTRAL_EXTENSION, RINGCENTRAL_PASSWORD);
            if (rc.token.access_token.Length > 0)
            {
                var requestParams = new ListMessagesParameters();
                requestParams.readStatus = new string[] { "Unread" };
                var resp = await rc.Restapi().Account().Extension().MessageStore().List(requestParams);
                int count = resp.records.Length;
                Console.WriteLine(String.Format("Retrieving a list of {0} messages.", count));
                foreach (var record in resp.records)
                {
                    var messageId = record.id;
                    var updateRequest = new UpdateMessageRequest();
                    updateRequest.readStatus = "Read";
                    var result = await rc.Restapi().Account().Extension().MessageStore(messageId).Put(updateRequest);
                    var readStatus = result.readStatus;
                    Console.WriteLine("Message status has been changed to " + readStatus);
                    break;
                }
            }
        }
        static private async Task retrieve_delete()
        {
            RestClient rc = new RestClient(RINGCENTRAL_CLIENTID, RINGCENTRAL_CLIENTSECRET, false);
            await rc.Authorize(RINGCENTRAL_USERNAME, RINGCENTRAL_EXTENSION, RINGCENTRAL_PASSWORD);
            if (rc.token.access_token.Length > 0)
            {
                var requestParams = new ListMessagesParameters();
                requestParams.readStatus = new string[] { "Read" };
                var resp = await rc.Restapi().Account().Extension().MessageStore().List(requestParams);
                int count = resp.records.Length;
                Console.WriteLine(String.Format("Get get a list of {0} messages.", count));
                foreach (var record in resp.records)
                {
                    var messageId = record.id;
                    await rc.Restapi().Account().Extension().MessageStore(messageId).Delete();
                    Console.WriteLine(String.Format("Message {0} has been deleted.", messageId));
                }
            }
        }
        static private async Task receive_reply()
        {
            RestClient rc = new RestClient(RINGCENTRAL_CLIENTID, RINGCENTRAL_CLIENTSECRET, false);
            await rc.Authorize(RINGCENTRAL_USERNAME, RINGCENTRAL_EXTENSION, RINGCENTRAL_PASSWORD);
            if (rc.token.access_token.Length > 0)
            {
                try
                {
                    var eventFilters = new[]
                    {
                        "/restapi/v1.0/account/~/extension/~/message-store/instant?type=SMS",
                        "/restapi/v1.0/account/~/extension/~/voicemail",
                    };
                    var subscription = new Subscription(rc, eventFilters, message =>
                    {
                        reply_sms_message(rc, message);
                    });
                    var subscriptionInfo = await subscription.Subscribe();
                    Console.WriteLine("Waiting for notifications ...");
                    while (waitLoop)
                    {
                        Thread.Sleep(1000);
                    }
                    await subscription.Revoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
        static private void reply_sms_message(RestClient rc, String message)
        {
            dynamic jsonObj = JObject.Parse(message);
            string eventType = jsonObj["event"];
            if (eventType.Contains("/message-store/instant"))
            {
                String senderNumber = jsonObj["body"]["from"]["phoneNumber"];
                Console.WriteLine("Recieved message from: " + senderNumber);
                var parameters = new CreateSMSMessage();
                parameters.from = new MessageStoreCallerInfoRequest { phoneNumber = RINGCENTRAL_USERNAME };
                parameters.to = new MessageStoreCallerInfoRequest[] { new MessageStoreCallerInfoRequest { phoneNumber = senderNumber } };
                parameters.text = "This is an automatic reply. Thank you for your message!";
                var resp = rc.Restapi().Account().Extension().Sms().Post(parameters);
                Console.WriteLine("Replied message sent.");
                waitLoop = false;
            }
            else if (eventType.Contains("/voicemail"))
            {
                String senderNumber = jsonObj["body"]["from"]["phoneNumber"];
                if (senderNumber != null)
                {
                    Console.WriteLine("Recieved a voicemail from: " + senderNumber);
                    var parameters = new CreateSMSMessage();
                    parameters.from = new MessageStoreCallerInfoRequest { phoneNumber = RINGCENTRAL_USERNAME };
                    parameters.to = new MessageStoreCallerInfoRequest[] { new MessageStoreCallerInfoRequest { phoneNumber = senderNumber } };
                    parameters.text = "This is an automatic reply. Thank you for your voice message! I will get back to you asap.";
                    var resp = rc.Restapi().Account().Extension().Sms().Post(parameters);
                    Console.WriteLine("Replied message sent.");
                    waitLoop = false;
                }
                else
                {
                    Console.WriteLine("Private call, no phone number to reply!");
                }
            }
            else
            {
                Console.WriteLine("Not an event we are waiting for.");
            }
        }
    }
}