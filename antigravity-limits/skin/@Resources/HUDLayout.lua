-- ==============================================================================
-- Antigravity Quota HUD v18 - Unlimited Accounts + Account Manager UI
-- Rainmeter Lua 5.1. UTF-8 without BOM.
-- ==============================================================================

local OWNED = {}
local ownedBuiltFor = 0

local G = {
    SKIN_W   = 320,
    PAD      = 18,
    BAR_W    = 134,
    COL2_X   = 168,
    HEADER_H = 38,
    ACC_GAP  = 11,
    SEC_GAP  = 15,
    FOOTER_H = 34,
    POLL_SEC = 300,
}

local themes = {
    obsidian = {
        name = "OBSIDIAN",
        bgRgb = "10,13,22", borderRgb = "255,255,255", borderAlpha = "20",
        titleRgb = "241,245,249", footerRgb = "226,232,240", heroRgb = "255,255,255",
        labelRgb = "218,228,240", subRgb = "195,208,225", mutedRgb = "203,213,225",
        resetRgb = "226,232,240", greenRgb = "52,211,153", amberRgb = "251,191,36",
        redRgb = "248,113,113", pillTextRgb = "203,213,225", btnTextRgb = "255,255,255",
        vars = {
            ColorBarTrack        = "255,255,255,22",
            ColorDivider         = "255,255,255,16",
            ColorPillBg          = "255,255,255,16",
            ColorPillBorder      = "255,255,255,32",
            ColorPillText        = "203,213,225,179",
            ColorPillBgHover     = "255,255,255,38",
            ColorPillBorderHover = "255,255,255,75",
            ColorPillTextHover   = "255,255,255,235",
            ColorBtnBg           = "255,255,255,16",
            ColorBtnBorder       = "255,255,255,32",
            ColorBtnText         = "255,255,255,204",
            ColorBtnBgHover      = "255,255,255,38",
            ColorBtnBorderHover  = "255,255,255,75",
            ColorBtnTextHover    = "255,255,255,235",
        },
    },
    frost = {
        name = "FROST",
        bgRgb = "246,248,251", borderRgb = "255,255,255", borderAlpha = "90",
        titleRgb = "30,41,59", footerRgb = "51,65,85", heroRgb = "30,41,59",
        labelRgb = "71,85,105", subRgb = "100,116,139", mutedRgb = "71,85,105",
        resetRgb = "51,65,85", greenRgb = "5,150,105", amberRgb = "217,119,6",
        redRgb = "220,38,38", pillTextRgb = "30,41,59", btnTextRgb = "30,41,59",
        vars = {
            ColorBarTrack        = "100,116,139,28",
            ColorDivider         = "100,116,139,25",
            ColorPillBg          = "255,255,255,25",
            ColorPillBorder      = "15,23,42,32",
            ColorPillText        = "30,41,59,179",
            ColorPillBgHover     = "255,255,255,65",
            ColorPillBorderHover = "15,23,42,75",
            ColorPillTextHover   = "15,23,42,235",
            ColorBtnBg           = "255,255,255,25",
            ColorBtnBorder       = "15,23,42,32",
            ColorBtnText         = "30,41,59,204",
            ColorBtnBgHover      = "255,255,255,65",
            ColorBtnBorderHover  = "15,23,42,75",
            ColorBtnTextHover    = "15,23,42,235",
        },
    },
}

local themeOrder = { "obsidian", "frost" }
local ACCENT_VAR = { Green = "ColorAccentGreen", Amber = "ColorAccentAmber", Red = "ColorAccentRed" }
local PCT_ACCENT_VAR = { Green = "ColorPctGreen", Amber = "ColorPctAmber", Red = "ColorPctRed" }

local state = { theme = "obsidian" }
local accs = {}
local lastSyncTime = os.time() - 720
local nextAutoPoll = os.time() + G.POLL_SEC

local function persist(key, val)
    local stateFile = SKIN:GetVariable("@") .. "UserState.inc"
    SKIN:Bang("!WriteKeyValue", "Variables", key, tostring(val), stateFile)
    SKIN:Bang("!SetVariable", key, tostring(val))
end

local function setOpt(meter, opt, value) SKIN:Bang("!SetOption", meter, opt, tostring(value)) end
local function setY(meter, y) SKIN:Bang("!SetOption", meter, "Y", tostring(math.floor(y + 0.5))) end
local function setX(meter, x) SKIN:Bang("!SetOption", meter, "X", tostring(math.floor(x + 0.5))) end
local function show(meter) SKIN:Bang("!ShowMeter", meter) end
local function hide(meter) SKIN:Bang("!HideMeter", meter) end

local function varNum(name, fb)
    local v = tonumber(SKIN:GetVariable(name, tostring(fb or 0)))
    if not v then return fb or 0 end
    return math.max(0, math.min(1, v))
end

local function pctNum(frac) return tostring(math.floor(frac * 100 + 0.5)) end

local function healthOf(frac)
    if frac >= 0.60 then return "Green" end
    if frac >= 0.25 then return "Amber" end
    return "Red"
end

local function resetLabel(accId, model, horizon)
    local s = SKIN:GetVariable("Acc" .. accId .. model .. horizon .. "Reset", "--")
    if s and s ~= "" then return s end
    return "--"
end

local function accountHasModel(accId, modelKey)
    if modelKey == "Claude" then
        if (tonumber(SKIN:GetVariable("ShowClaude", "1")) or 1) == 0 then return false end
        local accHas = tostring(SKIN:GetVariable("Acc" .. accId .. "HasClaude", "1"))
        return not (accHas == "0" or accHas == "false" or accHas == "no")
    elseif modelKey == "Gemini" then
        if (tonumber(SKIN:GetVariable("ShowGemini", "1")) or 1) == 0 then return false end
        local accHas = tostring(SKIN:GetVariable("Acc" .. accId .. "HasGemini", "1"))
        return not (accHas == "0" or accHas == "false" or accHas == "no")
    elseif modelKey == "Argon" then
        -- shown only when the API actually returned the Argon group for this account
        local accHas = tostring(SKIN:GetVariable("Acc" .. accId .. "HasArgon", "0"))
        return (accHas == "1" or accHas == "true")
    end
    return false
end

local function rebuildOwned(slotCount)
    local target = math.max(1, slotCount or 1)
    if target <= ownedBuiltFor then return end
    OWNED = {
        "MeterFooterDivider", "MeterFooterDot", "MeterFooterStatus",
        "MeterFooterSyncPill", "MeterFooterSyncBtn", "MeterFooterSyncClickZone",
    }
    for i = 1, target do
        local d = "MeterAcc" .. i
        for _, s in ipairs({
            "StatusDot", "Name", "Chev", "ClickZone",
            "Sum5HVal", "Sum5HLabel", "SumWkVal", "SumWkLabel", "SumReset",
            "NoModels", "Divider",
            "ClaudeSumVal", "ClaudeChev", "ClaudeClickZone",
            "GPinClickZone", "GRowClickZone", "CPinClickZone", "APinClickZone",
        }) do
            table.insert(OWNED, d .. s)
        end
        for _, f in ipairs({ { model = "G", key = "Gemini" }, { model = "C", key = "Claude" } }) do
            local p = d .. f.model
            table.insert(OWNED, d .. f.key .. "Label")
            table.insert(OWNED, d .. f.model .. "ColHead5H")
            table.insert(OWNED, d .. f.model .. "ColHeadWk")
            for _, h in ipairs({"FiveH", "Weekly"}) do
                table.insert(OWNED, p .. h .. "Val")
                table.insert(OWNED, p .. h .. "ResetVal")
                table.insert(OWNED, p .. h .. "Bar")
            end
        end
        -- Gemini 4 Argon: weekly-only section
        table.insert(OWNED, d .. "ArgonLabel")
        table.insert(OWNED, d .. "AColHeadWk")
        table.insert(OWNED, d .. "AWeeklyVal")
        table.insert(OWNED, d .. "AWeeklyResetVal")
        table.insert(OWNED, d .. "AWeeklyBar")
    end
    ownedBuiltFor = target
end

local function hideEverything()
    for _, m in ipairs(OWNED) do
        hide(m)
        setY(m, 0)
    end
end

local function applyTheme()
    local t = themes[state.theme] or themes.obsidian
    local bgAlpha = tonumber(SKIN:GetVariable("WidgetAlpha", "155")) or 155
    local textAlpha = math.max(60, math.min(255, tonumber(SKIN:GetVariable("TextAlpha", "175")) or 175))

    SKIN:Bang("!SetVariable", "ColorBgHud", t.bgRgb .. "," .. bgAlpha)
    SKIN:Bang("!SetVariable", "ColorBorderHud", t.borderRgb .. "," .. t.borderAlpha)

    local heroA   = math.floor(math.min(255, textAlpha * (179 / 175)) + 0.5)
    local smallA  = math.floor(math.min(255, textAlpha * (204 / 175)) + 0.5)
    local resetA  = 255
    local graphicAccentA = math.floor(math.min(255, textAlpha * (214 / 175)) + 0.5)

    SKIN:Bang("!SetVariable", "ColorTextHero", t.heroRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorTextTitle", t.titleRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorTextSectionLabel", t.labelRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorPctHero", t.heroRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorPctGreen", t.greenRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorPctAmber", t.amberRgb .. "," .. heroA)
    SKIN:Bang("!SetVariable", "ColorPctRed", t.redRgb .. "," .. heroA)

    SKIN:Bang("!SetVariable", "ColorTextFooter", t.footerRgb .. "," .. smallA)
    SKIN:Bang("!SetVariable", "ColorTextLabel", t.labelRgb .. "," .. smallA)
    SKIN:Bang("!SetVariable", "ColorTextSub", t.subRgb .. "," .. smallA)
    SKIN:Bang("!SetVariable", "ColorTextMuted", t.mutedRgb .. "," .. smallA)
    SKIN:Bang("!SetVariable", "ColorTextResetVal", t.resetRgb .. "," .. resetA)

    SKIN:Bang("!SetVariable", "ColorAccentGreen", t.greenRgb .. "," .. graphicAccentA)
    SKIN:Bang("!SetVariable", "ColorAccentAmber", t.amberRgb .. "," .. graphicAccentA)
    SKIN:Bang("!SetVariable", "ColorAccentRed", t.redRgb .. "," .. graphicAccentA)

    for k, v in pairs(t.vars) do
        SKIN:Bang("!SetVariable", k, v)
    end

    local pinnedRgb = (state.theme == "frost") and "2,132,199" or "56,189,248"
    SKIN:Bang("!SetVariable", "ColorTextPinned", pinnedRgb .. ",235")

    setOpt("MeterHeaderTitle", "FontColor", t.titleRgb .. "," .. heroA)
    setOpt("MeterHeaderToggleAllBtn", "FontColor", t.titleRgb .. "," .. smallA)
    setOpt("MeterFooterStatus", "FontColor", t.footerRgb .. "," .. smallA)

    setOpt("MeterHeaderThemeBtn", "Text", t.name)
    setOpt("MeterHeaderThemeBtn", "FontColor", t.pillTextRgb .. "," .. heroA)
    setOpt("MeterHeaderThemePill", "Shape",
        "Rectangle 0,0,58,18,9 | Fill Color " .. t.vars.ColorPillBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorPillBorder)
    setOpt("MeterHeaderAddBtn", "FontColor", t.pillTextRgb .. "," .. heroA)
    setOpt("MeterHeaderAddPill", "Shape",
        "Rectangle 0,0,22,18,9 | Fill Color " .. t.vars.ColorPillBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorPillBorder)
    setOpt("MeterFooterSyncBtn", "FontColor", t.btnTextRgb .. "," .. smallA)
    setOpt("MeterFooterSyncPill", "Shape",
        "Rectangle 0,0,26,18,9 | Fill Color " .. t.vars.ColorBtnBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorBtnBorder)
end

local function barTrack(w)
    return "Rectangle 0,0," .. w .. ",2.5,1.25 | Fill Color #ColorBarTrack# | StrokeWidth 0"
end
local function barFill(w, health)
    local fw = math.max(2, math.floor(w + 0.5))
    return "Rectangle 0,0," .. fw .. ",2.5,1.25 | Fill Color #" .. ACCENT_VAR[health] .. "# | StrokeWidth 0"
end

local function getActiveSummaryModel(a)
    if a.summaryModel == "G" and accountHasModel(a.id, "Gemini") then return "G"
    elseif a.summaryModel == "C" and accountHasModel(a.id, "Claude") then return "C"
    elseif a.summaryModel == "A" and accountHasModel(a.id, "Argon") then return "A" end
    if accountHasModel(a.id, "Gemini") then return "G"
    elseif accountHasModel(a.id, "Claude") then return "C"
    elseif accountHasModel(a.id, "Argon") then return "A" end
    return nil
end

local function placeMetric(accId, model, horizon, x, y, isAuthFailed)
    local p = "MeterAcc" .. accId .. model .. horizon

    setX(p .. "Val", x)
    setY(p .. "Val", y)
    if isAuthFailed then
        setOpt(p .. "Val", "Text", "--")
        setOpt(p .. "Val", "FontColor", "#ColorPctRed#")
    else
        local frac = varNum("Acc" .. accId .. model .. horizon, 1)
        local h = healthOf(frac)
        setOpt(p .. "Val", "Text", pctNum(frac) .. "%")
        if h == "Green" then
            setOpt(p .. "Val", "FontColor", "#ColorPctHero#")
        else
            setOpt(p .. "Val", "FontColor", "#" .. PCT_ACCENT_VAR[h] .. "#")
        end
    end
    show(p .. "Val")

    local resetTxt = isAuthFailed and "Token Expired" or (resetLabel(accId, model, horizon) .. " reset")
    setX(p .. "ResetVal", x)
    setY(p .. "ResetVal", y + 31)
    setOpt(p .. "ResetVal", "Text", resetTxt)
    setOpt(p .. "ResetVal", "FontColor", isAuthFailed and "#ColorPctRed#" or "#ColorTextResetVal#")
    show(p .. "ResetVal")

    local barW = G.BAR_W
    setX(p .. "Bar", x)
    setY(p .. "Bar", y + 48)
    setOpt(p .. "Bar", "Shape", barTrack(barW))
    if isAuthFailed then
        setOpt(p .. "Bar", "Shape2", "Rectangle 0,0," .. barW .. ",2.5,1.25 | Fill Color #ColorAccentRed# | StrokeWidth 0")
    else
        local frac = varNum("Acc" .. accId .. model .. horizon, 1)
        local h = healthOf(frac)
        local fillW = frac * barW
        setOpt(p .. "Bar", "Shape2", barFill(fillW, h))
    end
    show(p .. "Bar")
end

local function placeCollapsed(a, y)
    local d = "MeterAcc" .. a.id

    setY(d .. "StatusDot", y + 16); show(d .. "StatusDot")
    setY(d .. "Name", y + 5)
    setOpt(d .. "Name", "ClipString", "1")
    setOpt(d .. "Name", "W", "106")
    setOpt(d .. "Name", "H", "22")
    setOpt(d .. "Name", "Text", a.name)
    show(d .. "Name")
    setY(d .. "Chev", y + 6); setOpt(d .. "Chev", "Text", ">"); show(d .. "Chev")
    setY(d .. "ClickZone", y); show(d .. "ClickZone")

    if a.authFailed then
        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #ColorAccentRed# | StrokeWidth 0")
        setOpt(d .. "Name", "ToolTipText", a.name .. " (Token Expired - Open Account Manager to Refresh)")
        setOpt(d .. "ClickZone", "ToolTipText", a.name .. " (Token Expired - Open Account Manager to Refresh)")

        setY(d .. "Sum5HVal", y + 2)
        setOpt(d .. "Sum5HVal", "Text", "--")
        setOpt(d .. "Sum5HVal", "FontColor", "#ColorPctRed#")
        show(d .. "Sum5HVal")

        setY(d .. "Sum5HLabel", y + 17); show(d .. "Sum5HLabel")

        setY(d .. "SumWkVal", y + 2)
        setOpt(d .. "SumWkVal", "Text", "--")
        setOpt(d .. "SumWkVal", "FontColor", "#ColorPctRed#")
        show(d .. "SumWkVal")

        setY(d .. "SumWkLabel", y + 17); show(d .. "SumWkLabel")

        setY(d .. "SumReset", y + 6)
        setOpt(d .. "SumReset", "Text", "EXPIRED")
        setOpt(d .. "SumReset", "FontColor", "#ColorPctRed#")
        show(d .. "SumReset")

        return y + 36
    end

    setOpt(d .. "Name", "ToolTipText", a.name)
    setOpt(d .. "ClickZone", "ToolTipText", a.name)

    local topModel = getActiveSummaryModel(a)
    if topModel == "A" then
        local week = varNum("Acc" .. a.id .. "AWeekly", 1)
        local resetTxt = resetLabel(a.id, "A", "Weekly")
        local hw = healthOf(week)

        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #" .. ACCENT_VAR[hw] .. "# | StrokeWidth 0")

        setY(d .. "Sum5HVal", y + 2)
        setOpt(d .. "Sum5HVal", "Text", "--")
        setOpt(d .. "Sum5HVal", "FontColor", "#ColorTextMuted#")
        show(d .. "Sum5HVal")

        setY(d .. "Sum5HLabel", y + 17); show(d .. "Sum5HLabel")

        setY(d .. "SumWkVal", y + 2)
        setOpt(d .. "SumWkVal", "Text", pctNum(week) .. "%")
        setOpt(d .. "SumWkVal", "FontColor", "#" .. PCT_ACCENT_VAR[hw] .. "#")
        show(d .. "SumWkVal")

        setY(d .. "SumWkLabel", y + 17); show(d .. "SumWkLabel")

        setY(d .. "SumReset", y + 6)
        setOpt(d .. "SumReset", "Text", resetTxt)
        show(d .. "SumReset")
    elseif topModel then
        local five = varNum("Acc" .. a.id .. topModel .. "FiveH", 1)
        local week = varNum("Acc" .. a.id .. topModel .. "Weekly", 1)
        local resetTxt = resetLabel(a.id, topModel, "FiveH")

        local h5, hw = healthOf(five), healthOf(week)
        local worstOverall = math.min(five, week)
        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #" .. ACCENT_VAR[healthOf(worstOverall)] .. "# | StrokeWidth 0")

        setY(d .. "Sum5HVal", y + 2)
        setOpt(d .. "Sum5HVal", "Text", pctNum(five) .. "%")
        setOpt(d .. "Sum5HVal", "FontColor", "#" .. PCT_ACCENT_VAR[h5] .. "#")
        show(d .. "Sum5HVal")

        setY(d .. "Sum5HLabel", y + 17); show(d .. "Sum5HLabel")

        setY(d .. "SumWkVal", y + 2)
        setOpt(d .. "SumWkVal", "Text", pctNum(week) .. "%")
        setOpt(d .. "SumWkVal", "FontColor", "#" .. PCT_ACCENT_VAR[hw] .. "#")
        show(d .. "SumWkVal")

        setY(d .. "SumWkLabel", y + 17); show(d .. "SumWkLabel")

        setY(d .. "SumReset", y + 6)
        setOpt(d .. "SumReset", "Text", resetTxt)
        show(d .. "SumReset")
    else
        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #ColorTextMuted# | StrokeWidth 0")
        setY(d .. "SumReset", y + 6)
        setOpt(d .. "SumReset", "Text", "None")
        show(d .. "SumReset")
    end

    return y + 36
end

local function placeExpanded(a, y)
    local d = "MeterAcc" .. a.id

    setY(d .. "StatusDot", y + 14); show(d .. "StatusDot")
    setY(d .. "Name", y + 3)
    setOpt(d .. "Name", "ClipString", "1")
    setOpt(d .. "Name", "W", "250")
    setOpt(d .. "Name", "H", "22")
    setOpt(d .. "Name", "Text", a.name)
    show(d .. "Name")
    setY(d .. "Chev", y + 4); setOpt(d .. "Chev", "Text", "v"); show(d .. "Chev")
    setY(d .. "ClickZone", y); show(d .. "ClickZone")

    if a.authFailed then
        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #ColorAccentRed# | StrokeWidth 0")
        setOpt(d .. "Name", "ToolTipText", a.name .. " (Token Expired - Open Account Manager to Refresh)")
        setOpt(d .. "ClickZone", "ToolTipText", a.name .. " (Token Expired - Open Account Manager to Refresh)")
    else
        setOpt(d .. "Name", "ToolTipText", a.name)
        setOpt(d .. "ClickZone", "ToolTipText", a.name)
    end

    local hasG = accountHasModel(a.id, "Gemini")
    local hasC = accountHasModel(a.id, "Claude")

    local five, week = 1.0, 1.0
    if hasG and hasC then
        five = math.min(varNum("Acc" .. a.id .. "GFiveH", 1), varNum("Acc" .. a.id .. "CFiveH", 1))
        week = math.min(varNum("Acc" .. a.id .. "GWeekly", 1), varNum("Acc" .. a.id .. "CWeekly", 1))
    elseif hasG then
        five = varNum("Acc" .. a.id .. "GFiveH", 1); week = varNum("Acc" .. a.id .. "GWeekly", 1)
    elseif hasC then
        five = varNum("Acc" .. a.id .. "CFiveH", 1); week = varNum("Acc" .. a.id .. "CWeekly", 1)
    end

    local hasA = accountHasModel(a.id, "Argon")
    if hasA then week = math.min(week, varNum("Acc" .. a.id .. "AWeekly", 1)) end

    if not a.authFailed then
        local worstOverall = math.min(five, week)
        setOpt(d .. "StatusDot", "Shape", "Ellipse 0,0,2.5 | Fill Color #" .. ACCENT_VAR[healthOf(worstOverall)] .. "# | StrokeWidth 0")
    end

    local inner = y + 32
    local activeModel = getActiveSummaryModel(a)

    if hasG then
        local gLabel = d .. "GeminiLabel"
        setY(gLabel, inner)
        setOpt(gLabel, "FontColor", (activeModel == "G") and "#ColorTextPinned#" or "#ColorTextSectionLabel#")
        show(gLabel)

        setX(d .. "GPinClickZone", 16); setY(d .. "GPinClickZone", inner - 2); show(d .. "GPinClickZone")
        setX(d .. "GRowClickZone", 76); setY(d .. "GRowClickZone", inner - 2); show(d .. "GRowClickZone")

        setY(d .. "GColHead5H", inner + 21); setY(d .. "GColHeadWk", inner + 21)
        show(d .. "GColHead5H"); show(d .. "GColHeadWk")

        local metricY = inner + 34
        placeMetric(a.id, "G", "FiveH", G.PAD, metricY, a.authFailed)
        placeMetric(a.id, "G", "Weekly", G.COL2_X, metricY, a.authFailed)

        inner = metricY + 56
    end

    if hasC then
        if hasG then inner = inner + G.SEC_GAP end

        local cLabel = d .. "ClaudeLabel"
        setY(cLabel, inner)
        setOpt(cLabel, "FontColor", (activeModel == "C") and "#ColorTextPinned#" or "#ColorTextSectionLabel#")
        show(cLabel)

        setX(d .. "CPinClickZone", 16); setY(d .. "CPinClickZone", inner - 2); show(d .. "CPinClickZone")

        local cChev = d .. "ClaudeChev"
        local cZone = d .. "ClaudeClickZone"
        setY(cChev, inner + 2); setX(cZone, 112); setY(cZone, inner - 2)
        show(cChev); show(cZone)

        if a.claudeOpen then
            setOpt(cChev, "Text", "v")
            setY(d .. "CColHead5H", inner + 21); setY(d .. "CColHeadWk", inner + 21)
            show(d .. "CColHead5H"); show(d .. "CColHeadWk")

            local metricY = inner + 34
            placeMetric(a.id, "C", "FiveH", G.PAD, metricY, a.authFailed)
            placeMetric(a.id, "C", "Weekly", G.COL2_X, metricY, a.authFailed)

            inner = metricY + 56
        else
            setOpt(cChev, "Text", ">")
            local sumMeter = d .. "ClaudeSumVal"
            setY(sumMeter, inner + 2)
            if a.authFailed then
                setOpt(sumMeter, "Text", "-- / --")
                setOpt(sumMeter, "FontColor", "#ColorPctRed#")
            else
                local c5 = varNum("Acc" .. a.id .. "CFiveH", 1)
                local cw = varNum("Acc" .. a.id .. "CWeekly", 1)
                local hc = healthOf(math.min(c5, cw))
                setOpt(sumMeter, "Text", pctNum(c5) .. "% / " .. pctNum(cw) .. "%")
                setOpt(sumMeter, "FontColor", "#" .. PCT_ACCENT_VAR[hc] .. "#")
            end
            show(sumMeter)

            inner = inner + 20
        end
    end

    if hasA then
        if hasG or hasC then inner = inner + G.SEC_GAP end

        local aLabel = d .. "ArgonLabel"
        setY(aLabel, inner)
        setOpt(aLabel, "FontColor", (activeModel == "A") and "#ColorTextPinned#" or "#ColorTextSectionLabel#")
        show(aLabel)

        setX(d .. "APinClickZone", 16); setY(d .. "APinClickZone", inner - 2); show(d .. "APinClickZone")

        setX(d .. "AColHeadWk", G.PAD); setY(d .. "AColHeadWk", inner + 21)
        show(d .. "AColHeadWk")

        local metricY = inner + 34
        placeMetric(a.id, "A", "Weekly", G.PAD, metricY, a.authFailed)
        inner = metricY + 56
    end

    if not hasG and not hasC and not hasA then
        setY(d .. "NoModels", inner); show(d .. "NoModels")
        inner = inner + 22
    end

    return inner
end

local function updateFooterText()
    local diff = os.time() - lastSyncTime
    local str = ""
    if diff < 10 then str = "Synced just now"
    elseif diff < 60 then str = "Synced " .. diff .. "s ago"
    elseif diff < 3600 then str = "Synced " .. math.floor(diff / 60) .. "m ago"
    else str = "Synced " .. math.floor(diff / 3600) .. "h ago" end
    setOpt("MeterFooterStatus", "Text", str .. "  /  Poll: 5m")
end

local function loadAccountsFromVars()
    local rawCount = tonumber(SKIN:GetVariable("AccCount", "0")) or 0
    local count = math.max(0, rawCount)
    rebuildOwned(math.max(count, 3))
    local ep = tonumber(SKIN:GetVariable("LastSyncEpoch", "0")) or 0
    if ep > 0 then lastSyncTime = ep end

    accs = {}
    for i = 1, count do
        local defName = (i == 1) and "Work" or ((i == 2) and "Personal Pro" or ("Account " .. i))
        local vVaultIdx = tonumber(SKIN:GetVariable("Acc" .. i .. "VaultIdx", tostring(i))) or i
        local defOpen = (i == 1) and "1" or "0"
        local defClaude = (i == 1) and "1" or "0"
        local vOpen = tostring(SKIN:GetVariable("Acc" .. vVaultIdx .. "Open", defOpen))
        local vClaude = tostring(SKIN:GetVariable("Acc" .. vVaultIdx .. "ClaudeOpen", defClaude))
        local vSum = tostring(SKIN:GetVariable("Acc" .. vVaultIdx .. "SummaryModel", "AUTO")):upper()
        local vAuth = tostring(SKIN:GetVariable("Acc" .. i .. "AuthFailed", "0"))

        table.insert(accs, {
            id = i,
            vaultIdx = vVaultIdx,
            name = SKIN:GetVariable("Acc" .. i .. "Name", defName),
            authFailed = (vAuth == "1" or vAuth == "true"),
            open = (vOpen == "1" or vOpen == "true"),
            claudeOpen = (vClaude == "1" or vClaude == "true"),
            summaryModel = (vSum == "G" or vSum == "C" or vSum == "A") and vSum or nil,
        })
    end
end

local function layout()
    hideEverything()

    local y = G.HEADER_H

    if #accs == 0 then
        local d = "MeterAcc1"
        setY(d .. "NoModels", y + 10)
        setOpt(d .. "NoModels", "Text", "No accounts shown. Open Account Manager to select.")
        show(d .. "NoModels")
        y = y + 36
    else
        for i, a in ipairs(accs) do
            if a.open then
                y = placeExpanded(a, y)
            else
                y = placeCollapsed(a, y)
            end

            if i < #accs then
                local d = "MeterAcc" .. a.id
                setY(d .. "Divider", y + 8)
                show(d .. "Divider")
                y = y + G.ACC_GAP + 8
            end
        end
    end

    local footerY = y + 6
    setY("MeterFooterDivider", footerY)
    setY("MeterFooterDot", footerY + 15)
    setY("MeterFooterStatus", footerY + 5)
    setY("MeterFooterSyncPill", footerY + 6)
    setX("MeterFooterSyncBtn", 288)
    setY("MeterFooterSyncBtn", footerY + 3)
    setY("MeterFooterSyncClickZone", footerY + 3)

    updateFooterText()

    show("MeterFooterDivider")
    show("MeterFooterDot")
    show("MeterFooterStatus")
    show("MeterFooterSyncPill")
    show("MeterFooterSyncBtn")
    show("MeterFooterSyncClickZone")

    y = footerY + G.FOOTER_H

    setOpt("MeterBackground", "Shape",
        "Rectangle 0,0," .. G.SKIN_W .. "," .. y .. ",14" ..
        " | Fill Color #ColorBgHud# | StrokeWidth 0.75 | Stroke Color #ColorBorderHud#")
    SKIN:Bang("!SetVariable", "HUDHeight", tostring(y))

    local allOpen = (#accs > 0)
    for _, a in ipairs(accs) do
        if not a.open then allOpen = false end
    end
    setOpt("MeterHeaderToggleAllBtn", "Text", allOpen and "v" or ">")

    for _, m in ipairs(OWNED) do
        SKIN:Bang("!UpdateMeter", m)
    end
    SKIN:Bang("!UpdateMeter", "MeterBackground")
    SKIN:Bang("!UpdateMeter", "MeterHeaderTitle")
    SKIN:Bang("!UpdateMeter", "MeterHeaderAddBtn")
    SKIN:Bang("!UpdateMeter", "MeterHeaderThemeBtn")
    SKIN:Bang("!UpdateMeter", "MeterHeaderToggleAllBtn")
    SKIN:Bang("!Redraw")
end

local function runBridge(params)
    SKIN:Bang("!SetOption", "MeasureBridgeCmd", "Parameter", params)
    SKIN:Bang("!UpdateMeasure", "MeasureBridgeCmd")
    SKIN:Bang("!CommandMeasure", "MeasureBridgeCmd", "Run")
end

local function launchBridgeGui(params)
    local exe = SKIN:GetVariable("@") .. "AntigravityBridge.exe"
    SKIN:Bang('["' .. exe .. '" ' .. params .. ']')
end

function Initialize()
    state.theme = SKIN:GetVariable("Theme", "obsidian")
    if not themes[state.theme] then
        state.theme = "obsidian"
        persist("Theme", "obsidian")
    end
    loadAccountsFromVars()
    applyTheme()
    layout()
end

function Update()
    updateFooterText()
    if #accs > 0 and os.time() >= nextAutoPoll then
        nextAutoPoll = os.time() + G.POLL_SEC
        SKIN:Bang("!CommandMeasure", "MeasureAutoSync", "Run")
    end
end

function OnBridgeSync()
    lastSyncTime = os.time()
    nextAutoPoll = os.time() + G.POLL_SEC
    setOpt("MeterFooterSyncBtn", "Text", "#DefaultSyncIcon#")
    loadAccountsFromVars()
    layout()
end

function ManageAccounts() launchBridgeGui("manage " .. state.theme) end
function AddAccount() launchBridgeGui("add " .. state.theme) end
function RenameAccount(idx)
    local vIdx = (accs[idx] and accs[idx].vaultIdx) or idx
    launchBridgeGui("rename " .. tostring(vIdx) .. " " .. state.theme)
end
function RemoveAccount(idx)
    local vIdx = (accs[idx] and accs[idx].vaultIdx) or idx
    launchBridgeGui("remove " .. tostring(vIdx) .. " " .. state.theme)
end
function ActivateAccount(idx)
    local vIdx = (accs[idx] and accs[idx].vaultIdx) or idx
    runBridge("activate " .. tostring(vIdx))
end

function SyncNow()
    lastSyncTime = os.time()
    setOpt("MeterFooterSyncBtn", "Text", "...")
    setOpt("MeterFooterStatus", "Text", "Syncing...  /  Poll: 5m")
    SKIN:Bang("!UpdateMeter", "MeterFooterSyncBtn")
    SKIN:Bang("!UpdateMeter", "MeterFooterStatus")
    SKIN:Bang("!Redraw")
    SKIN:Bang("!CommandMeasure", "MeasureAutoSync", "Run")
end

function ToggleTheme()
    local nextTheme = (state.theme == "obsidian") and "frost" or "obsidian"
    state.theme = nextTheme
    persist("Theme", nextTheme)
    applyTheme()
    layout()
end

function SetTheme(name)
    local n = tostring(name or ""):lower()
    if themes[n] then
        state.theme = n
        persist("Theme", n)
        applyTheme()
        layout()
    end
end

function AdjustAlpha(delta)
    local a = tonumber(SKIN:GetVariable("WidgetAlpha", "155")) or 155
    a = math.max(30, math.min(255, a + (tonumber(delta) or 0)))
    persist("WidgetAlpha", a)
    applyTheme()
    layout()
end

function ToggleAcc(id)
    local n = tonumber(id)
    for _, a in ipairs(accs) do
        if a.id == n then
            a.open = not a.open
            persist("Acc" .. (a.vaultIdx or a.id) .. "Open", a.open and "1" or "0")
        end
    end
    layout()
end

function SetAccOpen(id, isOpen)
    local n = tonumber(id)
    local stateBool = (isOpen == 1 or isOpen == "1" or isOpen == true or isOpen == "true")
    for _, a in ipairs(accs) do
        if a.id == n then
            a.open = stateBool
            persist("Acc" .. (a.vaultIdx or a.id) .. "Open", a.open and "1" or "0")
        end
    end
    layout()
end

function SetSummaryModel(accId, modelKey)
    local n = tonumber(accId)
    local m = tostring(modelKey or ""):upper()
    for _, a in ipairs(accs) do
        if a.id == n then
            a.summaryModel = m
            persist("Acc" .. (a.vaultIdx or a.id) .. "SummaryModel", m)
        end
    end
    layout()
end

function ToggleAll()
    local allOpen = true
    for _, a in ipairs(accs) do
        if not a.open then allOpen = false end
    end
    for _, a in ipairs(accs) do
        a.open = not allOpen
        persist("Acc" .. (a.vaultIdx or a.id) .. "Open", a.open and "1" or "0")
    end
    layout()
end

function ToggleClaude(accId)
    local n = tonumber(accId)
    for _, a in ipairs(accs) do
        if a.id == n then
            a.claudeOpen = not a.claudeOpen
            persist("Acc" .. (a.vaultIdx or a.id) .. "ClaudeOpen", a.claudeOpen and "1" or "0")
        end
    end
    layout()
end

function ToggleClaudeVisibility()
    local cur = tonumber(SKIN:GetVariable("ShowClaude", "1")) or 1
    persist("ShowClaude", (cur == 1) and 0 or 1)
    layout()
end

function ToggleGeminiVisibility()
    local cur = tonumber(SKIN:GetVariable("ShowGemini", "1")) or 1
    persist("ShowGemini", (cur == 1) and 0 or 1)
    layout()
end