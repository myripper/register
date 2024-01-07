public async Task<bool> CheckExistDevice(string? deviceId, CancellationToken stoppingToken)
    {
        /*var response = await _scbApiRestClient.ExecuteAsync("/v1/config/frontend", null, stoppingToken, Method.Get);

        if (response?.Content == null)
        {
            return false;
        }*/
        
        await _scbApiRestClient.NewScbClient();
        
        var parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_tag" },
            { "tag", "ANDROID" }
        };

        var tag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_dtag" },
            { "device_id", _masterCache.DeviceId }
        };

        var strDtag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        var dtag = JsonSerializer.Deserialize<dynamic>(strDtag);
        
        //data = deviceid + jailbreak + userMode + tilesversion + isloadgeneralconsent + tag

        var data = $"{deviceId}0INDIVIDUAL761{tag}";
        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_payload" },
            { "data", data }
        };

        var encryptPayload = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        var json = new
        {
            dtag = dtag["dtag"],
            jailbreak = "0",
            userMode = "INDIVIDUAL",
            tilesVersion = "76",
            isLoadGeneralConsent = "1",
            payload = encryptPayload,
            tag = tag
        };

        var headers = new List<HeaderParameter>()
        {
            new("dsig", dtag["dsig"])
        };
        
        var response = await _scbApiRestClient.ExecuteAsync("v3/login/preloadandresumecheck", json, stoppingToken, Method.Post, headers);

        if (response?.Content == null)
        {
            return false;
        }
        
        var preload = JsonSerializer.Deserialize<dynamic>(response.Content);

        int.TryParse(preload["status"]["code"].ToString(), out int code);

        if (code != 1060)
        {
            _logger.LogError($"preloadandresumecheck {preload["status"]["description"]}");
            
            return false;
        }
        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_dtag" },
            { "device_id", _masterCache.DeviceId }
        };

        strDtag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        dtag = JsonSerializer.Deserialize<dynamic>(strDtag);
        
        var jsonMigration = new
        {
            dtag = dtag["dtag"],
            userMode = "INDIVIDUAL",
            tilesVersion = "76"
        };
        
        headers = new List<HeaderParameter>()
        {
            new("dsig", dtag["dsig"])
        };

        response = await _scbApiRestClient.ExecuteAsync("v1/login/getMigrationFlag", jsonMigration, stoppingToken, Method.Post, headers);

        if (response?.Content == null)
        {
            return false;
        }

        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "get_tag" },
            { "msisdn", _masterCache.MobileNo },
            { "session_id", _scbApiRestClient.SessionId }
        };

        tag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        var objectToDeserialize = new Susanoo(); 
        var xmlSerializer = new System.Xml.Serialization.XmlSerializer(objectToDeserialize.GetType());

        using TextReader reader = new StringReader(tag);
        
        var tagResponse = (Susanoo)xmlSerializer.Deserialize(reader)!;

        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_tag" },
            { "tag", "ANDROID" }
        };

        tag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        //deviceid + เบอร์โทรศัพท์ + jailbreak + userMode + tilesversion + isloadgeneralconsent + tag

        data = $"{deviceId}{_masterCache.MobileNo}0INDIVIDUAL761{tag}";
        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_payload" },
            { "data", data }
        };

        encryptPayload = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        parameters = new Dictionary<string, string>()
        {
            { "bank", "SCB" },
            { "type", "encrypt_dtag" },
            { "device_id", _masterCache.DeviceId }
        };

        strDtag = await _scbApiRestClient.ExecuteEncryptAsync(parameters, stoppingToken);
        
        dtag = JsonSerializer.Deserialize<dynamic>(strDtag);
        
        var jsonPreload = new
        {
            //deviceId = deviceId,
            dtag = dtag["dtag"],
            jailbreak = "0",
            mobileNo = _masterCache.MobileNo,
            userMode = "INDIVIDUAL",
            tilesVersion = "76",
            isLoadGeneralConsent = "1",
            payload = encryptPayload,
            tag = tag
        };
        
        headers = new List<HeaderParameter>()
        {
            new("dsig", dtag["dsig"])
        };

        response = await _scbApiRestClient.ExecuteAsync("v3/login/preloadandresumecheck", jsonPreload, stoppingToken, Method.Post, headers);

        if (response?.Content == null)
        {
            return false;
        }
        
        preload = JsonSerializer.Deserialize<dynamic>(response.Content);

        int.TryParse(preload["status"]["code"].ToString(), out code);

        switch (code)
        {
            case 1017:
                _logger.LogError("DeviceId Doesn't Exist!");

                break;
            case 3018:
            {
                _logger.LogInformation($"Term Condition!");

                var jsonAccept = new
                {
                    tcAccept = new
                    {
                        type = "EASYAPP"
                    }
                };

                response = await _scbApiRestClient.ExecuteAsync("v3/profiles/termcondversion", jsonAccept,
                    stoppingToken, Method.Put);

                var jsonTerm = JsonSerializer.Deserialize<dynamic>(response.Content);

                int.TryParse(jsonTerm["status"]["code"].ToString(), out code);
                break;
            }
            default:
                _logger.LogError($"preloadandresumecheck {preload["status"]["description"]}");
                return false;
        }

        return code == 1017;
    }
