#!/usr/bin/env python3
"""Validate CSV, then deterministically export typed Unity Resources configuration.
Does not change CSV, locale sources, research status, or publication gates.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from validate_config import ConfigValidator, TABLE_KEYS

ROOT = Path(__file__).resolve().parents[1]
DOUBLE = set('inner_length_cm inner_width_cm inner_height_cm gross_volume_l effective_volume_l_design game_bioload_capacity game_filter_capacity pre_first_confirm_refund_ratio game_bioload_per_individual startle_sensitivity01 recovery_min_real_s recovery_max_real_s approach_probability_min01 approach_probability_max01 approach_cooldown_real_s min_behavior_real_s min_stable_game_hours'.split())
INTEGER = set('purchase_price_coin units_per_purchase post_confirm_sell_price_per_purchase_coin game_school_min game_school_max amount_coin step_index purchase_count'.split())
BOOLEAN = set('simulation_ready supports_schooling requires_wet_tank repeatable required_for_v0'.split())
NAMES = {'tanks':'Tanks','items':'Items','species':'Species','plants':'Plants','behavior_profiles':'Behaviors','gates':'Gates','economy':'Economy','route':'Route','compatibility':'Compatibility'}

def field(name):
    return ''.join(word[:1].upper()+word[1:] for word in name.split('_'))

def build(root):
    report = ConfigValidator(root/'data').run()
    if not report['ok']:
        raise ValueError('\n'.join(report['errors']))
    digest=hashlib.sha256()
    payload={'SchemaVersion':1}
    for table in TABLE_KEYS:
        path=root/'data'/(table+'.csv')
        digest.update(table.encode()); digest.update(path.read_bytes())
        rows=[]
        with path.open(encoding='utf-8-sig',newline='') as source:
            for row in csv.DictReader(source):
                typed={}
                for key,value in row.items():
                    typed[field(key)]=float(value) if key in DOUBLE else int(value) if key in INTEGER else value=='true' if key in BOOLEAN else value
                rows.append(typed)
        payload[NAMES[table]]=rows
    payload['ContentVersion']=digest.hexdigest()
    target=root/'unity/Assets/_Game/Resources/Configuration/runtime.json'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(payload,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    return {'path':str(target),'content_version':payload['ContentVersion'],'records':report['records'],'pending':len(report['warnings'])}

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--root',type=Path,default=ROOT)
    print(json.dumps(build(parser.parse_args().root),ensure_ascii=False,indent=2))
