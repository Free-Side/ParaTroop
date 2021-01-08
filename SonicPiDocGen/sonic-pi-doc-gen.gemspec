Gem::Specification.new do |s|
  s.name        = 'sonic-pi-doc-gen'
  s.version     = '0.0.1'
  s.date        = '2020-12-15'
  s.summary     = "Sonic-Pi Doc Generator"
  s.description = "A tool to generate json files containing Sonic-Pi documentation HTML for use in ParaTroop."
  s.authors     = ["Paul Wheeler"]
  s.email       = 'paul@free-side.net'
  s.files       = Dir.glob("{bin,lib}/**/*")
  s.executables  = ['generate-lang-docs.rb']
  s.require_path = 'lib'
  s.homepage    = ''
  s.license       = 'MIT'
end
