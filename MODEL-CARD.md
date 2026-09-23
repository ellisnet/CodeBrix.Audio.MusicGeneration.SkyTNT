---
license: apache-2.0
datasets:
- projectlosangeles/Los-Angeles-MIDI-Dataset
- projectlosangeles/Monster-MIDI-Dataset
library_name: transformers
tags:
- music
- midi
- music generation
- midi generation
---
# Model Card for midi-model-tv2o-medium

Midi event transformer for music generation.

### Model Updates

- new tv2o-medium model: "tvo" means MidiTokenizerV2 optimised.

## Model Details

### Model Description

- **Developed by:** SkyTNT
- **Trained by:** SkyTNT
- **Model type:** Transformer
- **License:** apache-2.0

### Model Sources

- **Repository:** https://github.com/SkyTNT/midi-model
- **Demo:** https://huggingface.co/spaces/skytnt/midi-composer


## Training Details

### Training Data

- [projectlosangeles/Los-Angeles-MIDI-Dataset](https://huggingface.co/datasets/projectlosangeles/Los-Angeles-MIDI-Dataset)
- [projectlosangeles/Monster-MIDI-Dataset](https://huggingface.co/datasets/projectlosangeles/Monster-MIDI-Dataset)
- [SymphonyNet MIDI Dataset](https://symphonynet.github.io/)

#### Training Hyperparameters

- config: tv2o-medium
- max-len: 2048 then 4096
- lr: 1e-4 then 2e-4
- weight-decay: 0.01
- batch: 2x2x2
- bfloat16 precision

####  Loss
 - val loss: 0.2159

## Files

- model.ckpt: latest model checkpoint
- onnx/*.onnx: onnx format model