(function () {
    var authString = "";
    var connection = {};

    function ReceiveNewEvents(messageBlock) {
        var scrollBox = $("#eventBox");
        var isScrolledToBottom = scrollBox[0].scrollHeight - scrollBox[0].clientHeight <= scrollBox.scrollTop() + 1;

        messageBlock.messages.forEach(function (message) {
            var liElement = document.createElement('li');
            liElement.innerHTML = message.message;
            scrollBox.append(liElement);
        });

        if (isScrolledToBottom) {
            scrollBox.scrollTop(scrollBox[0].scrollHeight);
        }
    }

    function ReceiveNewDebugs(messageBlock) {
        var scrollBox = $("#debugBox");
        var isScrolledToBottom = scrollBox[0].scrollHeight - scrollBox[0].clientHeight <= scrollBox.scrollTop() + 1;

        messageBlock.messages.forEach(function (message) {
            var liElement = document.createElement('li');
            liElement.innerHTML = message.message;
            scrollBox.append(liElement);
        });

        if (isScrolledToBottom) {
            scrollBox.scrollTop(scrollBox[0].scrollHeight);
        }
    }

    function ApplyAuth(xhr) {
        xhr.setRequestHeader("Authorization", authString);
    }

    function HandleErrorResponse(response) {
        if (response.status && response.status === 401) {
            SetAuthStatus("None");
        }
    }

    function Authorize() {
        $.post({
            url: "/TASagentBotAPI/Auth/Authorize",
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({ Password: $("#input-password").val() }),
            success: async function (result) {
                authString = result.authString;

                SetAuthStatus(result.role);

                if (authString && authString.length > 0 &&
                    result.role === "Admin" || result.role === "Privileged") {
                    if (connection.invoke("Authenticate", authString)) {
                        //Clear Logs
                        $("#eventBox").empty();
                        $("#debugBox").empty();

                        //Request All Data
                        ReceiveNewEvents(await connection.invoke("RequestAllEvents"));
                        ReceiveNewDebugs(await connection.invoke("RequestAllDebugs"));
                    }

                    if (result.role === "Admin") {
                        FetchTimerValues();
                        FetchSavedTimerValues();
                        RefreshSerialDevices();
                    }
                }
            },
            error: function (result) { SetAuthStatus("None"); }
        });
    }


    function RefreshSerialDevices() {
        $.getJSON({
            url: "/TASagentBotAPI/ControllerSpy/GetPorts",
            headers: { "Authorization": "PASS NONE" },
            success: function (devices) {
                var serialDeviceSelect = $("#select-SerialDevice");

                serialDeviceSelect.empty();

                serialDeviceSelect.append($("<option value=\"\">None</option>"))

                devices.forEach(function (device) {
                    serialDeviceSelect.append($(`<option value="${device}">${device}</option>`));
                });

                UpdateCurrentSerialDevice();
            },
            beforeSend: ApplyAuth
        });
    }

    function UpdateCurrentSerialDevice() {
        $.getJSON({
            url: "/TASagentBotAPI/ControllerSpy/CurrentPort",
            headers: { "Authorization": "PASS NONE" },
            success: function (device) { $("#select-SerialDevice").val(device); },
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function SubmitSerialDeviceChanged(port) {
        PrintText(`Serial Device being changed to: ${port}`);
        $.post({
            url: "/TASagentBotAPI/ControllerSpy/Attach",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({ Port: port }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function RequestTimerLayoutDisplayModes() {
        $.getJSON({
            url: "/TASagentBotAPI/Timer/DisplayModes",
            success: function (result) {
                var timerMainValueSelect = $("#select-TimerMainValue");
                var timerSecondaryValueSelect = $("#select-TimerSecondaryValue");

                timerMainValueSelect.empty();
                timerSecondaryValueSelect.empty();

                result.forEach(function (timerLayoutValue) {
                    timerMainValueSelect.append($(`<option value="${timerLayoutValue.value}">${timerLayoutValue.display}</option>`));
                    timerSecondaryValueSelect.append($(`<option value="${timerLayoutValue.value}">${timerLayoutValue.display}</option>`));
                });
            },
            error: function HandleErrorResponse(response) {
                if (response.status && response.status === 404) {
                    //Timer probably not enabled - just delete it.
                    $("#nav-settings-timer-tab").remove();
                    $("#nav-settings-timer").remove();
                    $("#nav-tools-timer-tab").remove();
                    $("#nav-tools-timer").remove();
                }
            }
        });
    }

    function Quit() {
        $.post({
            url: "/TASagentBotAPI/Event/Quit",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function PrintText(message) {
        $.post({
            url: "/TASagentBotAPI/Event/Print",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({ Message: message }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function SubmitTimerTime(value) {
        PrintText(`Timer being changed to: ${value}`);
        $.post({
            url: "/TASagentBotAPI/Timer/Set",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({
                Time: parseFloat(value)
            }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function TriggerTimerAction(action) {
        $.post({
            url: `/TASagentBotAPI/Timer/${action}`,
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function FetchTimerValues() {
        $.get({
            url: "/TASagentBotAPI/Timer/TimerState",
            headers: { "Authorization": "PASS NONE" },
            success: function (timerState) {
                var lapStartTime = 0;
                timerState.laps.forEach(function (value) { lapStartTime += value; });

                $("#input-TimerCumulative").val(timeToString(lapStartTime + timerState.currentMS));
                $("#input-TimerCurrent").val(timeToString(timerState.currentMS));
                $("#input-TimerLap").val(timeToString(lapStartTime));
                $("#input-TimerLapCount").val(timerState.laps.length);

                $("#input-TimerMainLabel").val(timerState.layout.mainLabel);
                $("#select-TimerMainValue").val("" + timerState.layout.mainDisplay);
                $("#input-TimerSecondaryLabel").val(timerState.layout.secondaryLabel);
                $("#select-TimerSecondaryValue").val("" + timerState.layout.secondaryDisplay);
            },
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function FetchSavedTimerValues() {
        $.get({
            url: "/TASagentBotAPI/Timer/SavedTimers",
            headers: { "Authorization": "PASS NONE" },
            success: function (timers) {
                var timerLoadSelect = $("#select-TimerLoad");

                timerLoadSelect.empty();

                timers.forEach(function (timer) {
                    var cumulativeTime = timer.endingTime;
                    timer.laps.forEach(function (value) { cumulativeTime += value; });

                    var timerOptionElement = $(`<option value="${timer.name}">${timer.name}: ${timeToString(cumulativeTime)}</option>`);
                    timerLoadSelect.append(timerOptionElement);
                });

                $("#button-TimerLoad").prop("disabled", false);
            },
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function SubmitTimerLayout() {
        $.post({
            url: "/TASagentBotAPI/Timer/DisplayMode",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({
                MainLabel: $("#input-TimerMainLabel").val(),
                MainDisplay: parseInt($("#select-TimerMainValue").val()),
                SecondaryLabel: $("#input-TimerSecondaryLabel").val(),
                SecondaryDisplay: parseInt($("#select-TimerSecondaryValue").val())
            }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function LoadTimer(timer) {
        PrintText(`Loading Timer: ${timer}`);
        $.post({
            url: "/TASagentBotAPI/Timer/LoadTimer",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({
                TimerName: timer
            }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function SaveTimer(timer) {
        PrintText(`Saving Timer as: ${timer}`);
        $.post({
            url: "/TASagentBotAPI/Timer/SaveTimer",
            headers: { "Authorization": "PASS NONE" },
            contentType: "application/json;charset=utf-8",
            data: JSON.stringify({
                TimerName: timer
            }),
            beforeSend: ApplyAuth,
            error: HandleErrorResponse
        });
    }

    function SetAuthStatus(role) {
        var isUserAuth = false;
        var isPrivAuth = false;
        var isAdminAuth = false;

        switch (role) {
            case "None":
                break;

            case "User":
                isUserAuth = true;
                break;

            case "Privileged":
                isUserAuth = true;
                isPrivAuth = true;
                break;

            case "Admin":
                isUserAuth = true;
                isPrivAuth = true;
                isAdminAuth = true;
                break;

            default:
                console.log("Unexpected role: " + role);
                break;
        }

        $(".requireAuth").each(function () {
            $(this).prop('disabled', !isUserAuth);
        });

        $(".requirePrivAuth").each(function () {
            $(this).prop('disabled', !isPrivAuth);
        });

        $(".requireAdminAuth").each(function () {
            $(this).prop('disabled', !isAdminAuth);
        });

        $("#text-role").val(role);

        $(".userAuthGroup").each(function () {
            if (isUserAuth) {
                $(this).show();
            }
            else {
                $(this).hide();
            }
        });

        $(".privAuthGroup").each(function () {
            if (isPrivAuth) {
                $(this).show();
            }
            else {
                $(this).hide();
            }
        });

        $(".adminAuthGroup").each(function () {
            if (isAdminAuth) {
                $(this).show();
            }
            else {
                $(this).hide();
            }
        });
    }

    $(document).ready(function () {

        SetAuthStatus("None");

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/Hubs/Monitor")
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveNewDebugs', ReceiveNewDebugs);
        connection.on('ReceiveNewEvents', ReceiveNewEvents);

        connection.start();

        $("#button-Submit").click(Authorize);

        $("#button-Quit").click(Quit);

        $("#button-SendMessage").click(function () {
            PrintText($("#input-SendMessage").val());
            $("#input-SendMessage").val("");
        });

        $("#button-TimerSetTime").click(function () {
            SubmitTimerTime($("#input-TimerSetTime").val());
        });

        $("#button-TimerStart").click(function () {
            TriggerTimerAction("Start");
        });

        $("#button-TimerStop").click(function () {
            TriggerTimerAction("Stop");
        });

        $("#button-TimerReset").click(function () {
            TriggerTimerAction("Reset");
        });

        $("#button-TimerLap").click(function () {
            TriggerTimerAction("MarkLap");
        });

        $("#button-TimerUnlap").click(function () {
            TriggerTimerAction("UnmarkLap");
        });

        $("#button-TimerResetLap").click(function () {
            TriggerTimerAction("ResetCurrentLap");
        });

        $("#button-TimerLoad").click(function () {
            LoadTimer($("#select-TimerLoad").val());
        });

        $("#button-TimerSave").click(function () {
            SaveTimer($("#input-TimerSaveName").val());
        });

        $("#button-TimerFetchSaved").click(FetchSavedTimerValues);
        $("#button-TimerApplyLayout").click(SubmitTimerLayout);

        var serialDeviceSelect = $("#select-SerialDevice");
        serialDeviceSelect.change(function () { SubmitSerialDeviceChanged(serialDeviceSelect.val()); });

        //Trigger refresh on tab change
        //Tools tabs
        $("#nav-tools-timer-tab").click(FetchTimerValues).click(FetchSavedTimerValues);
        //Settings tabs
        $("#nav-settings-inputcapture-tab").click(RefreshSerialDevices);
        $("#nav-settings-timer-tab").click(FetchTimerValues);

        RequestTimerLayoutDisplayModes();
    });
})();