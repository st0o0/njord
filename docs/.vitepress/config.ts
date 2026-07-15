import { defineConfig } from 'vitepress'
import { withLikeC4 } from '@leberkas-org/vitepress-likec4'

export default withLikeC4({ likec4: { source: './likec4', height: '460px' } }, defineConfig({
  base: '/njord/',
  title: 'njord',
  description: 'Open-Meteo weather API → MQTT bridge for Home Assistant',
  head: [['link', { rel: 'icon', type: 'image/svg+xml', href: '/njord/logo.svg' }]],

  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Guide', link: '/getting-started' },
      { text: 'Config', link: '/configuration/' },
      { text: 'Models', link: '/models' },
      { text: 'Builder', link: '/builder' },
    ],

    sidebar: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Architecture', link: '/architecture' },
          { text: 'Home Assistant', link: '/home-assistant' },
        ],
      },
      {
        text: 'Configuration',
        items: [
          { text: 'Overview', link: '/configuration/' },
          { text: 'Locations', link: '/configuration/locations' },
          { text: 'Models', link: '/configuration/models' },
          { text: 'Horizons', link: '/configuration/horizons' },
          { text: 'Parameters', link: '/configuration/parameters' },
          { text: 'Enrichment', link: '/configuration/enrichment' },
          { text: 'MQTT', link: '/configuration/mqtt' },
          { text: 'Persistence', link: '/configuration/persistence' },
          { text: 'Budget', link: '/configuration/budget' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Model Catalog', link: '/models' },
          { text: 'MQTT Topics', link: '/mqtt-reference' },
          { text: 'Config Builder', link: '/builder' },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/st0o0/njord' },
    ],

    footer: {
      message: 'Open-Meteo data is licensed under CC BY 4.0.',
    },

    search: { provider: 'local' },
  },
}))
