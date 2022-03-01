using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class GeminiScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable[] ButtonSels;
    public KMSelectable GoSel;
    public TextMesh[] ScreenTexts;
    public TextMesh TimerText;
    public TextMesh IdText;
    public string ModuleName;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private int[] _startingNums = new int[3];
    private int[] _currentNums = new int[3];
    private int[] _functionOffsets = new int[3];

    private bool _inputStarted;
    private string _inputString;
    private int _timer;
    private bool _timerStarted;
    private int[] _inputNums = new int[3];
    private bool _gonnaStrike;
    private bool _hasFocus;

    private class GeminiInfo
    {
        public List<GeminiScript> CastorModules = new List<GeminiScript>();
        public List<GeminiScript> PolluxModules = new List<GeminiScript>();
    }
    private static readonly Dictionary<string, GeminiInfo> _infos = new Dictionary<string, GeminiInfo>();
    private GeminiInfo _info;
    private GeminiScript _partner;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);
        GoSel.OnInteract += GoPress;

        GetComponent<KMSelectable>().OnFocus += delegate { _hasFocus = true; };
        GetComponent<KMSelectable>().OnDefocus += delegate { _hasFocus = false; };

        var sn = BombInfo.GetSerialNumber();
        if (!_infos.ContainsKey(sn))
            _infos[sn] = new GeminiInfo();
        _info = _infos[sn];
        if (ModuleName == "Castor")
            _info.CastorModules.Add(this);
        if (ModuleName == "Pollux")
            _info.PolluxModules.Add(this);

        for (int i = 0; i < _startingNums.Length; i++)
        {
            _startingNums[i] = Rnd.Range(0, 1000);
            _currentNums[i] = _startingNums[i];
            ScreenTexts[i].text = _currentNums[i].ToString("000");
        }
        Debug.LogFormat("[{0} #{1}] Starting numbers: {2}", ModuleName, _moduleId, _startingNums.Join(", "));

        StartCoroutine(Init());
    }

    private void ResetModule()
    {
        _timerStarted = false;
        _inputStarted = false;
        _gonnaStrike = false;
        _inputString = "---------";

        _timer = Rnd.Range(30, 71);
        if (_partner != null && _partner._timer != 0)
            while (Math.Abs(_timer - _partner._timer) < 20)
                _timer = Rnd.Range(20, 81);
        TimerText.text = _timer.ToString();

        do
        {
            for (int i = 0; i < _functionOffsets.Length; i++)
                _functionOffsets[i] = Rnd.Range(1, 100);
        }
        while (_functionOffsets.Distinct().Count() != 3);

        for (int i = 0; i < 3; i++)
            Debug.LogFormat("[{0} #{1}] Function {2} offset: n {3} {4}.", ModuleName, _moduleId, "ABC"[i], ModuleName == "Castor" ? "+" : "-", _functionOffsets[i]);
        Debug.LogFormat("[{0} #{1}] Possible input: {2}", ModuleName, _moduleId, _functionOffsets.Select(i => (ModuleName == "Pollux" ? i * _timer % 1000 : (1000 - i) * _timer % 1000).ToString("000")).Join(" "));
    }

    private IEnumerator Init()
    {
        // Give all modules a chance to add themselves to the CastorModules/PolluxModules lists
        yield return null;

        // Discover my own partner
        var ix = (ModuleName == "Castor" ? _info.CastorModules : _info.PolluxModules).IndexOf(this);
        var pList = ModuleName == "Pollux" ? _info.CastorModules : _info.PolluxModules;
        _partner = pList.Count > ix ? pList[ix] : null;

        if (_partner != null)
            Debug.LogFormat("[{0} #{1}] This module has a partner!", ModuleName, _moduleId);
        else
            Debug.LogFormat("[{0} #{1}] This module does not have a partner.", ModuleName, _moduleId);

        // Display ID
        var idTxt = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[ix / 26].ToString() + "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[ix % 26].ToString();
        IdText.text = idTxt;
        Debug.LogFormat("[{0} #{1}] This module's ID is {2}", ModuleName, _moduleId, idTxt);

        ResetModule();
        StartCoroutine(CycleText());
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ButtonSels[btn].transform);
            ButtonSels[btn].AddInteractionPunch(0.5f);
            if (_moduleSolved || _timerStarted)
                return false;
            if (!_inputStarted)
                _inputStarted = true;
            _inputString = _inputString.Substring(1) + btn;
            for (int i = 0; i < ScreenTexts.Length; i++)
                ScreenTexts[i].text = _inputString.Substring(i * 3, 3);
            return false;
        };
    }

    private bool GoPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, GoSel.transform);
        GoSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _timerStarted || !_inputStarted)
            return false;
        for (int i = 0; i < ScreenTexts.Length; i++)
        {
            if (!int.TryParse(ScreenTexts[i].text, out _inputNums[i]) || _inputNums[i] < 0)
            {
                Module.HandleStrike();
                Debug.LogFormat("[{0} #{1}] Attempted to start the timer before entering all digits. Strike.", ModuleName, _moduleId);
                _inputStarted = false;
                return false;
            }
            _timerStarted = true;
        }
        return false;
    }

    private IEnumerator CycleText()
    {
        while (!_moduleSolved)
        {
            int time = (int) BombInfo.GetTime();
            while (time == (int) BombInfo.GetTime())
                yield return null;

            if (!_inputStarted)
            {
                for (int i = 0; i < _currentNums.Length; i++)
                {
                    _currentNums[i] = FunctionEncode(_currentNums[i], i);
                    ScreenTexts[i].text = _currentNums[i].ToString("000");
                }
                goto checks;
            }

            if (!_timerStarted)
                goto checks;

            if (_timer == 0)
            {
                if (_inputNums.Distinct().Count() == 1)
                {
                    _moduleSolved = true;
                    Module.HandlePass();
                    Debug.LogFormat("[{0} #{1}] All three numbers are equal the end of the timer. Module solved.", ModuleName, _moduleId);
                    yield break;
                }
                else
                    _gonnaStrike = true;
            }
            else
            {
                _timer--;
                TimerText.text = _timer.ToString();

                for (int i = 0; i < _currentNums.Length; i++)
                {
                    _inputNums[i] = FunctionEncode(_inputNums[i], i);
                    ScreenTexts[i].text = _inputNums[i].ToString("000");
                }
            }

            checks:
            // Give the partner a chance to solve or mark _gonnaStrike
            yield return null;

            if (_gonnaStrike)
            {
                Module.HandleStrike();
                Debug.LogFormat("[{0} #{1}] The three numbers ({2}) are not equal at the end of the timer. Strike.", ModuleName, _moduleId, _inputNums.Join(", "));
            }
            else if (_partner != null && _partner._moduleSolved)
            {
                Module.HandleStrike();
                Debug.LogFormat("[{0} #{1}] This module’s partner solved prematurely. Strike.", ModuleName, _moduleId);
                _partner = null;
            }
            else if (_partner != null && _partner._gonnaStrike && _timerStarted)
                Debug.LogFormat("[{0} #{1}] This module’s partner’s timer has run out and struck. Reset.", ModuleName, _moduleId);
            else
                continue;

            // Reset the module
            yield return null;
            ResetModule();
        }
    }

    private int FunctionEncode(int num, int ix)
    {
        int val = num;
        switch (ModuleName)
        {
            case "Castor":
                val += _functionOffsets[ix];
                break;
            case "Pollux":
                val -= _functionOffsets[ix];
                break;
        }
        return (val + 1000) % 1000;
    }

    private void Update()
    {
        if (_hasFocus)
            for (int i = 0; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                    ButtonSels[i].OnInteract();
    }
}
