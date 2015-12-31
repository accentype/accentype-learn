$(function () {
    AddAntiForgeryToken = function (data) {
        data.__RequestVerificationToken = $('input[name=__RequestVerificationToken]').val();
        return data;
    };

    predicting = function () {
        $('#icoThinking').show();
        $('#icoArrow').hide();
    };

    predicted = function () {
        $('#icoThinking').hide();
        $('#icoArrow').show();
    };

    var wordChoices;


    var seps = {};
    seps[","] = true;
    seps["."] = true;
    seps["?"] = true;
    seps[";"] = true;
    seps[":"] = true;

    getSegments = function (para) {
        segments = [];
        separators = [];

        s = []; // current segment
        for (var i = 0; i < para.length; i++) {
            if (seps[para[i]]) {

                if (s.length > 0) {
                    segments.push(s.join(''));
                }

                separators.push(para[i]);

                s = [];
            }
            else {
                s.push(para[i]);
            }
        }
        if (s.length > 0) {
            segments.push(s.join(''));
        }

        return [segments, separators];
    }

    predict = function (userQuery) {
        $('#spError').hide();

        predicting();

        var pieces = getSegments(userQuery);
        var segments = pieces[0];
        var separators = pieces[1];

        $.ajax({
            url: '/Home/Predict/',
            type: "POST",
            data: AddAntiForgeryToken({
                language: $('#spLanguage').text(),
                query: segments.join('.')
            }),
            dataType: "json",
            cache: false,
            success: function (data) {
                wordChoices = [];

                $('#divPredict').empty();

                var iSeparator = 0;
                for (var iS = 0; iS < data.length; iS++) {

                    // add segment
                    for (var i = 0; i < data[iS].length; i++) {
                        var wordText = data[iS][i][0] + ((i != data[iS].length - 1) ? ' ' : '');
                        $('#divPredict').append('<span id="sppw' + wordChoices.length + '" class="spPredictWord">' + wordText + '</span>');
                        wordChoices.push(data[iS][i]);
                    }

                    //add separator
                    if (iSeparator < separators.length) {
                        $('#divPredict').append(separators[iSeparator] + '&nbsp;');
                        iSeparator += 1;
                    }
                }
            },
            error: function (jqXHR, textStatus, errorThrown) {
                $('#spError').text('Could not perform prediction due to error: ' + errorThrown);
                $('#spError').show();
            },
            complete: function (jqXHR, textStatus) {
                predicted();
            }
        });
    }

    $('#txtQuery').keypress(function (event) {
        var keyCode = (event.which ? event.which : event.keyCode);
        
        var userQuery = $('#txtQuery').val();
        
        if (keyCode == 10 || keyCode == 13 && event.ctrlKey) { // Ctrl + Enter
            predict(userQuery);
        }
        else if (
            keyCode == 44 || // ,
            keyCode == 46 || // .
            keyCode == 63) { // ?

            predict(userQuery + String.fromCharCode(keyCode));
        }
    });

    var curWordControl;
    $('#divPredict').on({
        mouseenter: function () {

            curWordControl = $(this);

            var id = $(this).attr('id').substring(4);

            var divChoices = $('#divChoices');

            divChoices.empty();

            var choices = wordChoices[id];
            for (var i = 0; i < Math.min(choices.length, 10); i++) {
                divChoices.append('<p class="ut-text-choice">' + choices[i] + '</p>');
            }
            var rect = this.getBoundingClientRect();
            var left = rect.left - 4;
            var top = rect.top - 6;
            divChoices.css('left', left + 'px');
            divChoices.css('top', top + 'px');
            divChoices.show();
        },
    }, '.spPredictWord');

    $('#divChoices').mouseleave(function () {
        $(this).hide();
    });

    $('#divChoices').on('click', '.ut-text-choice', function () {
        var newText = $(this).text();
        if (curWordControl.text().length == newText.length + 1) {
            newText += ' ';
        }
        curWordControl.text(newText);
        $('#divChoices').hide();
    });

    $('.aLanguageChoice').click(function () {
        $('#spLanguage').text($(this).text());
    });

    var client = new ZeroClipboard(document.getElementById("btnCopy"));

    client.on("ready", function (readyEvent) {
        client.on("copy", function (event) {
            var clipboard = event.clipboardData;
            clipboard.setData("text/plain", $('#divPredict').text());
        });
        client.on("aftercopy", function (event) {
            $('#spCopyResult').css({ opacity: 1.0, visibility: "visible" }).animate({ opacity: 0 }, 1500);
        });
    });
});