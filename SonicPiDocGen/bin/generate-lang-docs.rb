#!/usr/bin/env ruby

require 'sonicpi/lang/core'
require 'sonicpi/lang/sound'
require 'sonicpi/synths/synthinfo'
require 'json'

SynthInfo = SonicPi::Synths::SynthInfo

documentation = {
  "synths" => SynthInfo.synth_doc_html_map,
  "fx" => SynthInfo.fx_doc_html_map,
  "samples" => SynthInfo.samples_doc_html_map,
  "lang" => SonicPi::Lang::Core.docs_html_map.merge(SonicPi::Lang::Sound.docs_html_map)
}

def function_autocomplete(doc)
  {
    "arguments" => doc[:args],
    "options" => doc[:opts],
    "block?" => doc[:accepts_block] ? (doc[:requires_block] ? "Required" : "Optional") : "None"
  }
end

autocomplete = {
  "synths" => Hash[SynthInfo.all_synths.map { |k| [k, SynthInfo.get_info(k).arg_defaults.keys] }],
  "fx" => Hash[SonicPi::Synths::SynthInfo.all_fx.map { |k| [k, SynthInfo.get_info("fx_" + k.to_s).arg_defaults.keys] }],
  "samples" => SynthInfo.all_samples,
  "lang" => SonicPi::Lang::Core.docs.to_a.concat(SonicPi::Lang::Sound.docs.to_a).map { |k, v| [k, function_autocomplete(v)] }
}

print JSON.pretty_generate({"documentation" => documentation, "autocomplete" => autocomplete})