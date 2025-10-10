# Model Mode & Cost Estimator - Quick Guide

## Overview
The Image To Pose Generator now includes intelligent model selection and real-time cost estimation to help you balance quality and pricing.

## Using the Mode Selector

### Where to Find It
On the **Input** step, look for the panel on the right side labeled **"Operating Mode"**.

### Available Modes

#### 🟦 Budget Mode
```
Fast & cheapest; ok for simple photos.
Model Priority: gpt-4.1-nano → gpt-4.1-mini
Output: ~300 tokens per step
```

**When to Use:**
- Simple, straightforward poses
- High-volume processing
- Testing and experimentation
- Budget constraints are primary concern

**Trade-offs:**
- May miss subtle details
- Less consistent with complex poses
- Good enough for basic poses

---

#### 🟩 Balanced Mode (Default)
```
Good quality for most cases.
Model Priority: gpt-4.1-mini → o4-mini → gpt-4.1
Output: ~600 tokens per step
```

**When to Use:**
- Most everyday use cases
- Standard character poses
- When quality matters but cost is a consideration
- Production work with reliable results

**Trade-offs:**
- Middle ground on pricing
- Very reliable quality
- Recommended for most users

---

#### 🟨 Quality Mode
```
Best quality at a sensible price.
Model Priority: gpt-4.1 → o4-mini
Output: ~800 tokens per step
```

**When to Use:**
- Complex, challenging poses
- Unusual angles or perspectives
- Maximum accuracy required
- Final production work

**Trade-offs:**
- Higher cost per run
- Best possible results
- Worth it for difficult poses

---

## Understanding Cost Estimates

### What You'll See

The estimates panel shows two sections:

```
Estimates (≈ tokens → USD)

Step 1 (Vision)
Input: 1,234 tokens
Output: 600 tokens
≈ $0.001234

Step 2 (Text)
Input: 567 tokens
Output: 600 tokens
≈ $0.000567
```

### What the Numbers Mean

**Input Tokens** = What you're sending to the API
- Step 1: Image tiles + your rough pose text
- Step 2: The extended pose description

**Output Tokens** = What the API sends back
- Estimated based on your chosen mode (300/600/800)
- Updates to actual count after API call

**USD Cost** = Approximate cost in US dollars
- Based on `config/pricing.json` rates
- Shows 6 decimal places (e.g., $0.000123)
- ≈ symbol means "approximately"

### Image Token Calculation

Images are converted to tokens using OpenAI's tile method:

```
Image: 1024×1024 pixels
Tiles: 2×2 = 4 tiles
Tokens: 70 base + (140 × 4) = 630 tokens
```

The app automatically reads your image dimensions and calculates this for you.

---

## Model Resolution

### What Happens After API Key Validation

1. App queries OpenAI's `/v1/models` API
2. Filters available models based on your key's access
3. Tries each model in priority order (for your selected mode)
4. Tests the model with a "ping" request
5. Falls back if preferred model isn't available
6. Displays the resolved model: **"Using: gpt-4.1-mini"**

### If Your Preferred Model Isn't Available

The app will automatically select the next best option:

```
Mode: Balanced
Preferred: gpt-4.1-mini (not available)
Fallback: o4-mini (available) ✓
Display: "Using: o4-mini"
```

The UI shows which model you're actually using, so there are no surprises.

---

## Updating Pricing Rates

### Initial Setup
The first time you run the app, it creates `config/pricing.json` next to the executable with approximate default rates.

### Keeping Rates Current

1. Click the **"OpenAI Pricing"** button in the app
2. Browser opens to https://openai.com/api/pricing
3. Check current rates for your models
4. Edit `config/pricing.json` with updated values
5. Restart app or change mode to reload rates

### Example pricing.json
```json
{
  "gpt-4.1-nano": {
    "input_per_million": 0.10,
    "output_per_million": 0.40
  },
  "gpt-4.1-mini": {
    "input_per_million": 0.40,
    "output_per_million": 1.60
  },
  "gpt-4.1": {
    "input_per_million": 2.50,
    "output_per_million": 10.00
  },
  "o4-mini": {
    "input_per_million": 3.00,
    "output_per_million": 12.00
  }
}
```

**Rates are in USD per 1 million tokens.**

---

## Live Estimate Updates

Estimates automatically recalculate when you:
- ✅ Change operating mode
- ✅ Select a different image
- ✅ Type in the rough pose description
- ✅ Validate your API key (resolves model)

This gives you instant feedback on the approximate cost before clicking "Process".

---

## Important Notes

### ⚠️ Estimates Are Approximate
- Token counts can vary slightly
- OpenAI may preprocess images differently
- Actual output length depends on complexity
- **Always check your OpenAI billing dashboard for exact charges**

### 💡 Tips for Cost Management

1. **Start with Balanced mode** for most work
2. **Use Budget mode** for testing prompts
3. **Switch to Quality** only when needed
4. **Monitor the estimates** before processing
5. **Update pricing.json** monthly to stay current

### 🔒 Privacy
- Estimates are computed locally
- No data sent to third parties
- Only API calls go to OpenAI
- Pricing file stored next to executable

---

## Example Workflow

1. **Launch app** → Validate API key
2. **Mode resolves** → "Using: gpt-4.1-mini"
3. **Select image** → Estimates show image tokens
4. **Type rough pose** → Estimates update with text tokens
5. **Review estimates** → See Step 1: ~$0.0015, Step 2: ~$0.0008
6. **Click Process** → Run Step 1, estimates update with actuals
7. **Continue workflow** → Step 2 estimates ready

---

## Troubleshooting

### "No models available"
- Check your API key has correct permissions
- Verify internet connection
- Ensure OpenAI API is accessible

### "Estimates show 0 tokens"
- Make sure image is loaded (preview visible)
- Type some text in rough pose field
- API key must be validated first

### "High costs shown"
- Very large images = more tiles = higher cost
- Check your mode selection
- Consider Budget mode for testing
- Verify pricing.json has correct rates

### "Model changed unexpectedly"
- Your key may not have access to preferred model
- App automatically picked best available fallback
- This is normal and expected behavior

---

## Support & Resources

- **OpenAI Pricing**: https://openai.com/api/pricing
- **Model List**: https://platform.openai.com/docs/models
- **API Keys**: https://platform.openai.com/api-keys
- **Vision Guide**: https://platform.openai.com/docs/guides/images-vision

For issues or questions, check the main README.md in the repository.

---

**Happy pose generating! 🎭**
